using Microsoft.EntityFrameworkCore;
using OrderApi.Application.DTOs;
using OrderApi.Application.Interfaces;
using OrderApi.Domain.Exceptions;

namespace OrderApi.Application.Services;

public class ReportService : IReportService
{
    private readonly IAppDbContext _db;

    public ReportService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TopProductResponse>> GetTopProductsAsync(int days, CancellationToken ct = default)
    {
        if (days < 1 || days > 365)
            throw new ValidationException("days", "days must be between 1 and 365.");

        var cutoff = DateTime.UtcNow.AddDays(-days);

        var results = await _db.OrderItems
            .AsNoTracking()
            .Where(i => i.Order.OrderDate >= cutoff)
            .GroupBy(i => new { i.ProductId, i.Product.Name })
            .Select(g => new TopProductResponse
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name,
                UnitsSold = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.LineTotal)
            })
            .OrderByDescending(r => r.Revenue)
            .ThenByDescending(r => r.UnitsSold)
            .ThenBy(r => r.ProductId)
            .Take(5)
            .ToListAsync(ct);

        return results;
    }
}
