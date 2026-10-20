using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderApi.Tests.Fixtures;

namespace OrderApi.Tests.Integration;

[Collection(nameof(ApiCollection))]
public class OrderTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public OrderTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // Reset Orders and OrderItems between tests; restore Products to exact seed values.
    public async Task InitializeAsync()
    {
        using var db = _factory.CreateDbContext();
        await db.OrderItems.ExecuteDeleteAsync();
        await db.Orders.ExecuteDeleteAsync();
        // Restore stock AND price — some tests mutate prices (e.g. UnitPrice snapshot test)
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

    // ── Test 1: Insufficient stock ─────────────────────────────────────────────

    [Fact]
    public async Task Post_InsufficientStock_Returns409WithFailingItems_AndStockUnchanged()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await db.Products.Where(p => p.Id == 3)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 2));

        var body = new { customerId = 1, items = new[] { new { productId = 3, quantity = 5 } } };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", body);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(409, json.GetProperty("status").GetInt32());

        var failingItems = json.GetProperty("failingItems").EnumerateArray().ToList();
        Assert.Single(failingItems);
        Assert.Equal(3,  failingItems[0].GetProperty("productId").GetInt32());
        Assert.Equal(5,  failingItems[0].GetProperty("requested").GetInt32());
        Assert.Equal(2,  failingItems[0].GetProperty("available").GetInt32());

        var stockAfter = await db.Products.Where(p => p.Id == 3).Select(p => p.StockQuantity).FirstAsync();
        Assert.Equal(2, stockAfter);

        var orderCount = await db.Orders.CountAsync();
        Assert.Equal(0, orderCount);
    }

    // ── Test 2: Invalid quantity ───────────────────────────────────────────────

    [Fact]
    public async Task Post_QuantityZero_Returns400WithErrorsDictionary()
    {
        // Arrange
        var body = new { customerId = 1, items = new[] { new { productId = 1, quantity = 0 } } };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(400, json.GetProperty("status").GetInt32());
        var errors = json.GetProperty("errors");
        Assert.True(errors.TryGetProperty("items[0].quantity", out _));
    }

    [Fact]
    public async Task Post_QuantityNegative_Returns400WithErrorsDictionary()
    {
        // Arrange
        var body = new { customerId = 1, items = new[] { new { productId = 1, quantity = -3 } } };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(400, json.GetProperty("status").GetInt32());
        var errors = json.GetProperty("errors");
        Assert.True(errors.TryGetProperty("items[0].quantity", out _));
    }

    // ── Test 3: Total calculation + UnitPrice snapshot ────────────────────────

    [Fact]
    public async Task Post_TotalIsCorrect_AndUnitPriceIsSnapshotted()
    {
        // Arrange — product 2 price=29.99, product 3 price=89.99
        var body = new
        {
            customerId = 1,
            items = new[]
            {
                new { productId = 2, quantity = 3 },  // 3 * 29.99 = 89.97
                new { productId = 3, quantity = 2 }   // 2 * 89.99 = 179.98
            }
        };
        // expected total = 89.97 + 179.98 = 269.95

        // Act
        var createResp = await _client.PostAsJsonAsync("/api/orders", body);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var order = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orderId = order.GetProperty("id").GetInt32();

        Assert.Equal(269.95m, order.GetProperty("totalAmount").GetDecimal());

        // Change product 3's price in the DB
        using var db = _factory.CreateDbContext();
        await db.Products.Where(p => p.Id == 3)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 999.00m));

        // GET the order again — UnitPrice must still be the original 89.99
        var getResp = await _client.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var fetched = await getResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(269.95m, fetched.GetProperty("totalAmount").GetDecimal());

        var item3 = fetched.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("productId").GetInt32() == 3);
        Assert.Equal(89.99m, item3.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(179.98m, item3.GetProperty("lineTotal").GetDecimal());
    }

    // ── Test 4: Concurrency — stock 1, 10 parallel orders ────────────────────

    [Fact]
    public async Task Post_ConcurrentOrders_StockOne_ExactlyOne201()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await db.Products.Where(p => p.Id == 18)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 1));

        var body = new { customerId = 1, items = new[] { new { productId = 18, quantity = 1 } } };

        // Act — 10 concurrent requests
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            _client.PostAsJsonAsync("/api/orders", body)).ToList();
        var responses = await Task.WhenAll(tasks);

        // Assert
        var created  = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflict = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, created);
        Assert.Equal(9, conflict);

        var finalStock = await db.Products.AsNoTracking()
            .Where(p => p.Id == 18).Select(p => p.StockQuantity).FirstAsync();
        Assert.Equal(0, finalStock);

        var itemCount = await db.OrderItems.CountAsync(i => i.ProductId == 18);
        Assert.Equal(1, itemCount);
    }

    // ── Test 5: Concurrency — stock 5, 10 parallel orders of qty 1 ───────────

    [Fact]
    public async Task Post_ConcurrentOrders_StockFive_ExactlyFive201()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await db.Products.Where(p => p.Id == 9)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 5));

        var body = new { customerId = 2, items = new[] { new { productId = 9, quantity = 1 } } };

        // Act
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            _client.PostAsJsonAsync("/api/orders", body)).ToList();
        var responses = await Task.WhenAll(tasks);

        // Assert
        var created  = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflict = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(5,  created);
        Assert.Equal(5,  conflict);

        var finalStock = await db.Products.AsNoTracking()
            .Where(p => p.Id == 9).Select(p => p.StockQuantity).FirstAsync();
        Assert.Equal(0, finalStock);

        var itemCount = await db.OrderItems.CountAsync(i => i.ProductId == 9);
        Assert.Equal(5, itemCount);
    }

    // ── Test 6: GET missing order → 404 ProblemDetails ────────────────────────

    [Fact]
    public async Task Get_MissingOrder_Returns404ProblemDetailsWithTraceId()
    {
        // Act
        var response = await _client.GetAsync("/api/orders/999999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(404, json.GetProperty("status").GetInt32());
        Assert.True(json.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrEmpty(traceId.GetString()));
    }

    // ── Test 7: Pagination ─────────────────────────────────────────────────────

    [Fact]
    public async Task Get_Pagination_ReturnsCorrectPage()
    {
        // Arrange — create 7 orders for customer 5
        var body = new { customerId = 5, items = new[] { new { productId = 2, quantity = 1 } } };
        for (int i = 0; i < 7; i++)
            await _client.PostAsJsonAsync("/api/orders", body);

        // Act — page 3 of pageSize 3 should have 1 item
        var response = await _client.GetAsync("/api/orders?customerId=5&pageSize=3&page=3");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(7, json.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, json.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, json.GetProperty("items").GetArrayLength());
    }
}
