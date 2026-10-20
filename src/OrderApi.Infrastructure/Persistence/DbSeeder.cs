using Microsoft.EntityFrameworkCore;
using OrderApi.Domain.Entities;

namespace OrderApi.Infrastructure.Persistence;

/// <summary>
/// Seeds past orders on startup in Development only.
/// Uses DateTime.UtcNow so orders always fall within the last 60 days.
/// Also reduces product stock to keep data consistent.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedOrdersAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Orders.AnyAsync(ct))
            return;

        var now = DateTime.UtcNow;
        var rng = new Random(42); // fixed seed for reproducibility within a run

        // (customerId, productId, quantity) tuples for 28 past orders
        var orderSpecs = new[]
        {
            (1, new[] { (1, 1), (2, 2) }),
            (2, new[] { (3, 1) }),
            (3, new[] { (4, 1), (5, 3) }),
            (4, new[] { (6, 2) }),
            (5, new[] { (7, 1) }),
            (6, new[] { (8, 1), (9, 1) }),
            (7, new[] { (10, 1) }),
            (8, new[] { (2, 5), (12, 3) }),
            (9, new[] { (14, 2) }),
            (10, new[] { (15, 1), (13, 1) }),
            (1, new[] { (16, 1) }),
            (2, new[] { (17, 1), (3, 1) }),
            (3, new[] { (5, 2) }),
            (4, new[] { (6, 1) }),
            (5, new[] { (1, 1) }),
            (6, new[] { (8, 2) }),
            (7, new[] { (12, 10) }),
            (8, new[] { (4, 1) }),
            (9, new[] { (7, 1), (9, 2) }),
            (10, new[] { (14, 1) }),
            (1, new[] { (2, 3) }),
            (2, new[] { (5, 1), (6, 1) }),
            (3, new[] { (3, 2) }),
            (4, new[] { (15, 4) }),
            (5, new[] { (16, 2) }),
            (6, new[] { (17, 1) }),
            (7, new[] { (8, 1), (2, 2) }),
            (8, new[] { (9, 3) }),
        };

        // Load the products we'll need and build a price map
        var productIds = orderSpecs
            .SelectMany(o => o.Item2.Select(i => i.Item1))
            .Distinct()
            .ToList();

        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var orders = new List<Order>();

        for (int idx = 0; idx < orderSpecs.Length; idx++)
        {
            var (customerId, items) = orderSpecs[idx];

            // Spread evenly across the last 60 days
            var daysAgo = (int)Math.Round(60.0 * idx / (orderSpecs.Length - 1));
            // Negative offset only — avoids seeding orders with future timestamps
            var orderDate = now.AddDays(-daysAgo).AddHours(rng.Next(-8, 0));

            var order = new Order
            {
                CustomerId = customerId,
                OrderDate = orderDate
            };

            foreach (var (productId, quantity) in items)
            {
                var product = products[productId];
                var orderItem = new OrderItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = product.Price
                };
                orderItem.CalculateLineTotal();
                order.Items.Add(orderItem);
            }

            order.CalculateTotal();
            orders.Add(order);
        }

        db.Orders.AddRange(orders);
        await db.SaveChangesAsync(ct);

        // Reduce stock for each seeded order so data stays consistent
        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                await db.Products
                    .Where(p => p.Id == item.ProductId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - item.Quantity), ct);
            }
        }
    }
}
