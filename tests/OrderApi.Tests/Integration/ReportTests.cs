using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderApi.Tests.Fixtures;

namespace OrderApi.Tests.Integration;

[Collection(nameof(ApiCollection))]
public class ReportTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ReportTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var db = _factory.CreateDbContext();
        await db.OrderItems.ExecuteDeleteAsync();
        await db.Orders.ExecuteDeleteAsync();
        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE Products SET
                StockQuantity = CASE Id
                    WHEN 1  THEN 50   WHEN 2  THEN 200 WHEN 3  THEN 75
                    WHEN 4  THEN 30   WHEN 5  THEN 150 WHEN 6  THEN 60
                    WHEN 7  THEN 40   WHEN 8  THEN 80  WHEN 9  THEN 100
                    WHEN 10 THEN 15   WHEN 11 THEN 10  WHEN 12 THEN 300
                    WHEN 13 THEN 120  WHEN 14 THEN 90  WHEN 15 THEN 180
                    WHEN 16 THEN 55   WHEN 17 THEN 70  WHEN 18 THEN 1
                    WHEN 19 THEN 0    WHEN 20 THEN 1
                    ELSE StockQuantity END,
                Price = CASE Id
                    WHEN 1  THEN 1299.99 WHEN 2  THEN 29.99  WHEN 3  THEN 89.99
                    WHEN 4  THEN 349.99  WHEN 5  THEN 49.99  WHEN 6  THEN 69.99
                    WHEN 7  THEN 199.99  WHEN 8  THEN 119.99 WHEN 9  THEN 34.99
                    WHEN 10 THEN 499.99  WHEN 11 THEN 699.99 WHEN 12 THEN 12.99
                    WHEN 13 THEN 19.99   WHEN 14 THEN 39.99  WHEN 15 THEN 24.99
                    WHEN 16 THEN 44.99   WHEN 17 THEN 54.99  WHEN 18 THEN 79.99
                    WHEN 19 THEN 27.99   WHEN 20 THEN 249.99
                    ELSE Price END");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Test 8: Report returns max 5 rows, sorted by revenue desc ─────────────

    [Fact]
    public async Task Get_TopProducts_ReturnsFiveRowsSortedByRevenue()
    {
        // Arrange — create orders for 6 products with known revenues:
        // Product 1  (1299.99): qty 3 -> revenue 3899.97  <- highest
        // Product 10 ( 499.99): qty 2 -> revenue  999.98
        // Product 4  ( 349.99): qty 2 -> revenue  699.98
        // Product 7  ( 199.99): qty 2 -> revenue  399.98
        // Product 8  ( 119.99): qty 2 -> revenue  239.98
        // Product 3  (  89.99): qty 2 -> revenue  179.98  <- 6th, must be excluded
        var orders = new[]
        {
            new { customerId = 1, items = new[] { new { productId = 1,  quantity = 3 } } },
            new { customerId = 2, items = new[] { new { productId = 10, quantity = 2 } } },
            new { customerId = 3, items = new[] { new { productId = 4,  quantity = 2 } } },
            new { customerId = 4, items = new[] { new { productId = 7,  quantity = 2 } } },
            new { customerId = 5, items = new[] { new { productId = 8,  quantity = 2 } } },
            new { customerId = 6, items = new[] { new { productId = 3,  quantity = 2 } } },
        };

        foreach (var o in orders)
        {
            var r = await _client.PostAsJsonAsync("/api/orders", o);
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }

        // Act
        var response = await _client.GetAsync("/api/reports/top-products?days=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rows = await response.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        Assert.NotNull(rows);
        Assert.Equal(5, rows.Count);

        // Top row should be product 1 with revenue 3899.97 and unitsSold 3
        var top = rows[0];
        Assert.Equal(1,       top.GetProperty("productId").GetInt32());
        Assert.Equal(3,       top.GetProperty("unitsSold").GetInt32());
        Assert.Equal(3899.97m, top.GetProperty("revenue").GetDecimal());

        // Verify descending revenue order
        var revenues = rows.Select(r => r.GetProperty("revenue").GetDecimal()).ToList();
        Assert.Equal(revenues.OrderByDescending(x => x).ToList(), revenues);

        // Product 3 (6th by revenue) must not appear
        Assert.DoesNotContain(rows, r => r.GetProperty("productId").GetInt32() == 3);
    }
}
