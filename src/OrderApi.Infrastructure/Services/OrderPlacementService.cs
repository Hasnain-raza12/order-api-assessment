using Microsoft.EntityFrameworkCore;
using OrderApi.Application.Interfaces;
using OrderApi.Domain.Entities;
using OrderApi.Domain.Exceptions;
using OrderApi.Infrastructure.Persistence;

namespace OrderApi.Infrastructure.Services;

public class OrderPlacementService : IOrderPlacementService
{
    private readonly AppDbContext _db;

    public OrderPlacementService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Order> PlaceOrderAsync(
        int customerId,
        IReadOnlyList<OrderItemInput> items,
        CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // Sort by ProductId ascending to avoid deadlocks between concurrent orders
            var sortedItems = items.OrderBy(i => i.ProductId).ToList();
            var productIds = sortedItems.Select(i => i.ProductId).ToList();

            // Read current price and stock inside the transaction
            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .OrderBy(p => p.Id)
                .ToListAsync(ct);

            var productMap = products.ToDictionary(p => p.Id);

            // Check all stock shortfalls up front — reject with full list
            var shortfalls = sortedItems
                .Where(i => productMap[i.ProductId].StockQuantity < i.Quantity)
                .Select(i => new StockShortfall(i.ProductId, i.Quantity, productMap[i.ProductId].StockQuantity))
                .ToList();

            if (shortfalls.Count > 0)
                throw new InsufficientStockException(shortfalls);

            // Deduct stock with conditional UPDATE per product.
            // ExecuteUpdateAsync does NOT check RowVersion — the WHERE clause is the oversell guard.
            // RowVersion catches optimistic conflicts on the SaveChanges path below.
            foreach (var item in sortedItems)
            {
                var rowsAffected = await _db.Products
                    .Where(p => p.Id == item.ProductId && p.StockQuantity >= item.Quantity)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - item.Quantity),
                        ct);

                if (rowsAffected == 0)
                {
                    // Someone else took the stock between our read and our update
                    var current = await _db.Products
                        .Where(p => p.Id == item.ProductId)
                        .Select(p => p.StockQuantity)
                        .FirstAsync(ct);

                    throw new InsufficientStockException(
                        [new StockShortfall(item.ProductId, item.Quantity, current)]);
                }
            }

            var order = new Order
            {
                CustomerId = customerId,
                OrderDate = DateTime.UtcNow
            };

            foreach (var item in sortedItems)
            {
                var unitPrice = productMap[item.ProductId].Price;
                var orderItem = new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = unitPrice
                };
                orderItem.CalculateLineTotal();
                order.Items.Add(orderItem);
            }

            order.CalculateTotal();

            _db.Orders.Add(order);

            try
            {
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                throw new ConcurrencyConflictException("A concurrency conflict occurred while saving the order. Please retry.");
            }

            return order;
        });
    }
}
