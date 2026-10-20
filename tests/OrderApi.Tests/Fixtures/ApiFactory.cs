using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderApi.Infrastructure.Persistence;

namespace OrderApi.Tests.Fixtures;

/// <summary>
/// Shared WebApplicationFactory. One instance per test collection.
/// Drops and recreates OrderDb_Tests before the first test runs.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Set the connection string via env var BEFORE Program.cs runs.
    // WebApplicationFactory uses HostFactoryResolver which executes Program.cs to build the
    // host; ConfigureAppConfiguration callbacks fire too late for AddInfrastructure's null
    // check. ASP.NET Core picks up ConnectionStrings__DefaultConnection automatically.
    static ApiFactory()
    {
        var connStr = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
            ?? "Server=localhost;Database=OrderDb_Tests;Trusted_Connection=True;TrustServerCertificate=True";
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connStr);
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync(); // applies migrations + HasData seed (customers + products)
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Testing environment prevents DbSeeder from running
        builder.UseEnvironment("Testing");
    }

    /// <summary>Returns a fresh DbContext scoped to OrderDb_Tests for Arrange/Assert steps.</summary>
    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}

/// <summary>xUnit collection so all test classes share one ApiFactory instance.</summary>
[CollectionDefinition(nameof(ApiCollection))]
public class ApiCollection : ICollectionFixture<ApiFactory> { }
