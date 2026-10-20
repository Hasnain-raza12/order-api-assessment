using Microsoft.EntityFrameworkCore;
using OrderApi.Application.DTOs;
using OrderApi.Application.Interfaces;
using OrderApi.Domain.Exceptions;

namespace OrderApi.Application.Services;

public class OrderService : IOrderService
{
    private readonly IAppDbContext _db;
    private readonly IOrderPlacementService _placement;

    public OrderService(IAppDbContext db, IOrderPlacementService placement)
    {
        _db = db;
        _placement = placement;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new ValidationException("items", "At least one item is required.");

        var errors = new Dictionary<string, string[]>();
        for (int i = 0; i < request.Items.Count; i++)
        {
            if (request.Items[i].Quantity <= 0)
                errors[$"items[{i}].quantity"] = [$"Quantity must be greater than zero."];
        }
        if (errors.Count > 0)
            throw new ValidationException(errors);

        // Merge duplicate productIds
        var merged = request.Items
            .GroupBy(x => x.ProductId)
            .Select(g => new OrderItemInput(g.Key, g.Sum(x => x.Quantity)))
            .ToList();

        var customerExists = await _db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new ValidationException("customerId", $"Customer {request.CustomerId} does not exist.");

        var requestedIds = merged.Select(x => x.ProductId).ToList();
        var foundIds = await _db.Products
            .Where(p => requestedIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);

        var missingIds = requestedIds.Except(foundIds).ToList();
        if (missingIds.Count > 0)
            throw new ValidationException("items", $"Products not found: {string.Join(", ", missingIds)}.");

        var order = await _placement.PlaceOrderAsync(request.CustomerId, merged, ct);

        return await GetOrderAsync(order.Id, ct);
    }

    public async Task<OrderResponse> GetOrderAsync(int id, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrderResponse
            {
                Id = o.Id,
                OrderDate = o.OrderDate,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer.Name,
                TotalAmount = o.TotalAmount,
                Items = o.Items.Select(i => new OrderItemResponse
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.LineTotal
                }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (order is null)
            throw new NotFoundException($"Order {id} was not found.");

        return order;
    }

    public async Task<PagedResponse<OrderSummaryResponse>> GetOrdersAsync(OrdersQueryRequest query, CancellationToken ct = default)
    {
        if (query.Page < 1)
            throw new ValidationException("page", "Page must be >= 1.");
        if (query.PageSize < 1 || query.PageSize > 100)
            throw new ValidationException("pageSize", "PageSize must be between 1 and 100.");
        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            throw new ValidationException("from", "'from' must be less than or equal to 'to'.");

        var q = _db.Orders.AsNoTracking();

        if (query.CustomerId.HasValue)
            q = q.Where(o => o.CustomerId == query.CustomerId.Value);
        if (query.From.HasValue)
            q = q.Where(o => o.OrderDate >= query.From.Value);
        if (query.To.HasValue)
        {
            // If "to" is date-only (time = midnight), include the full day
            var toExclusive = query.To.Value.TimeOfDay == TimeSpan.Zero
                ? query.To.Value.Date.AddDays(1)
                : query.To.Value;
            q = q.Where(o => o.OrderDate < toExclusive);
        }

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new OrderSummaryResponse
            {
                Id = o.Id,
                OrderDate = o.OrderDate,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer.Name,
                TotalAmount = o.TotalAmount
            })
            .ToListAsync(ct);

        return new PagedResponse<OrderSummaryResponse>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}
