# Order API

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- SQL Server (any edition — Developer, Express, or LocalDB all work)
- EF Core CLI tool

## Install the EF Core tool

```bash
dotnet tool install --global dotnet-ef
```

If it is already installed, update it:

```bash
dotnet tool update --global dotnet-ef
```

## Connection strings

The app reads its connection string from `src/OrderApi.Api/appsettings.Development.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=OrderDb;Trusted_Connection=True;TrustServerCertificate=True"
}
```

Tests read from `tests/OrderApi.Tests/appsettings.json` (same format, database `OrderDb_Tests`).

Both use Windows authentication against the default local instance (`localhost`). If your SQL Server runs as a named instance, change only the `Server` part:

| Instance type | Server value |
|---|---|
| Default (used above) | `localhost` |
| Named instance | `.\SQLEXPRESS` |
| LocalDB | `(localdb)\MSSQLLocalDB` |

## Create and seed the database

```bash
dotnet ef database update --project src/OrderApi.Infrastructure --startup-project src/OrderApi.Api
```

This creates `OrderDb`, applies the migration, and seeds **10 customers** and **20 products** via `HasData`.

Sample orders (≈ 28) spread over the last 60 days are added automatically on the **first run of the API in Development** by `DbSeeder`. Stock levels are adjusted to match. The seeder only runs once — if the Orders table is already populated it does nothing.

## Run the API

```bash
dotnet run --project src/OrderApi.Api
```

Swagger is available at **http://localhost:5163/swagger** (Development only).

## Run the tests

```bash
dotnet test tests/OrderApi.Tests
```

Tests run against a separate `OrderDb_Tests` database on the same SQL Server instance. The database is **dropped and recreated automatically** before the test run — no manual setup required.
