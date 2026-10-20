# AI Prompt History

This file is my log of how I used AI on this assessment: what I asked, what came back, and what I changed or rejected.

## Tools used

- **Claude Code**: implemented the solution, ran builds, migrations and tests, and committed to this repo.
- **Claude (chat)**: used alongside it to break the brief into structured prompts. I reviewed and edited every prompt before sending it, and the decisions and corrections below are mine.

## Before Claude Code

Before writing any code I made these decisions and built them into the first prompt:

- I asked for **Clean Architecture**. I kept it lightweight on purpose (Domain, Application, Infrastructure, Api) and told the AI not to add MediatR, CQRS, AutoMapper, generic repositories or a Unit of Work wrapper. For this scope they would only add code without adding value.
- I replaced the generic opening line with a **senior .NET engineer persona** and a short description of the business context.
- The first draft used **Docker** (docker-compose for SQL Server and Testcontainers for tests). I have SQL Server installed locally and use SSMS 20, so I removed Docker. The app and tests run against my local default instance with Windows authentication, and tests use a separate `OrderDb_Tests` database so dev data is never touched.
- I used `localhost` instead of my machine name in connection strings so the repo works on a reviewer's machine without edits.
- I decided the work should go step by step (plan first, then one part at a time with a commit after each) so the history shows the process.

---

## Prompt 1: Requirements and plan only

<details>
<summary>Full prompt</summary>

```text
Act as a senior .NET backend engineer , building ASP.NET Core APIs on SQL Server, mostly e-commerce and inventory systems. You write practical, production-ready code, keep the layering clean without overdoing it, handle transactions and concurrency properly, and verify things by actually building and running them. If something is a trade-off or an assumption, say so.

Context: I'm building the backend for a small e-commerce back office. Customers place orders with multiple products, and stock levels must always stay accurate.

Read everything below first. Don't write any code in this reply. I only want a plan, then wait for me to say "go".

STACK
- ASP.NET Core Web API on .NET 8, using controllers
- SQL Server (installed locally, default instance, Windows authentication), EF Core 8 with code-first migrations
- xUnit for tests, with WebApplicationFactory, running against my local SQL Server
- Swagger in Development
- No Docker

ARCHITECTURE
Lightweight clean architecture, 4 projects, dependencies point inward only.

1. src/OrderApi.Domain
   - Entities: Customer, Product, Order, OrderItem
   - Exceptions: NotFoundException, ValidationException, InsufficientStockException, ConcurrencyConflictException
   - No external packages
   - Order calculates its own total, OrderItem calculates its LineTotal

2. src/OrderApi.Application
   - References Domain only
   - DTOs, IOrderService, IReportService and their implementations, input validation
   - IAppDbContext interface (can reference EF Core abstractions so services can use IQueryable)

3. src/OrderApi.Infrastructure
   - References Application
   - AppDbContext, entity configurations, migrations, seed data
   - AddInfrastructure() extension for DI
   - Stock deduction / concurrency logic lives here, behind an interface defined in Application

4. src/OrderApi.Api
   - Thin controllers, no business logic, they just call services
   - Exception handler, ProblemDetails setup, Swagger, Program.cs

5. tests/OrderApi.Tests
   - Integration tests against the API

Don't add MediatR, CQRS, AutoMapper, generic repositories or a unit of work wrapper. DbContext is already the unit of work and this scope doesn't need the extra layers.

DATABASE
1. Customers
   - Id, Name, Email (unique)
   - Seed around 10

2. Products
   - Id, Name, Sku (unique), Price decimal(18,2), StockQuantity int, RowVersion (rowversion, concurrency token)
   - CHECK constraint StockQuantity >= 0
   - Seed around 20, a few with stock 0 or 1 so stock cases are easy to test

3. Orders
   - Id, CustomerId (FK), OrderDate datetime2 in UTC, TotalAmount decimal(18,2)
   - Indexes on (CustomerId, OrderDate) and on OrderDate

4. OrderItems
   - Id, OrderId (FK, cascade delete), ProductId (FK, restrict)
   - Quantity int with CHECK > 0
   - UnitPrice decimal(18,2), this is the price at the time of purchase
   - LineTotal decimal(18,2)
   - Index on ProductId

5. Seeding
   - Use HasData so it goes through migrations and `dotnet ef database update` creates and seeds everything
   - Also seed some past orders spread over the last 60 days, otherwise the report endpoint has nothing to show

ENDPOINTS

1. POST /api/orders
   Body: { "customerId": 1, "items": [ { "productId": 1, "quantity": 2 } ] }
   - Empty items list -> 400
   - Any quantity 0 or negative -> 400 (ValidationProblemDetails, say which item)
   - Same productId sent twice -> merge them and add up the quantities
   - Customer doesn't exist -> 400
   - Any product doesn't exist -> 400, list the missing product ids
   - Any item doesn't have enough stock -> reject the whole order with 409, include productId, requested and available for each failing item. Nothing gets saved.
   - On success, all in one transaction:
     - reduce stock
     - store UnitPrice from the product's current price
     - calculate LineTotal and TotalAmount on the server (never take prices from the client)
   - Return 201 Created with a Location header pointing to GET /api/orders/{id}

2. GET /api/orders/{id}
   - Return id, order date, customerId, customer name, total
   - Items with productId, product name, quantity, unitPrice, lineTotal
   - 404 if not found
   - Use AsNoTracking and project with Select

3. GET /api/orders
   - Filters: customerId, from, to, page, pageSize
   - page default 1, pageSize default 20, max 100
   - from must be <= to
   - Invalid values -> 400
   - If a date-only value is passed for "to", include that whole day
   - Sort by OrderDate desc, then Id desc
   - Response: { items, page, pageSize, totalCount, totalPages }
   - Both the count and the page query run in the database

4. GET /api/reports/top-products?days=30
   - days default 30, allowed range 1 to 365, otherwise 400
   - Top 5 products by revenue in the last N days
   - Return productId, productName, unitsSold, revenue
   - Ties: unitsSold desc, then productId
   - This has to run in the database. GroupBy + OrderByDescending + Take(5) that EF translates to SQL, or raw SQL. No ToList() before the aggregation.
   - Log the generated SQL so I can check it and put it in the README

ERROR HANDLING
1. Use IExceptionHandler with AddProblemDetails()
2. Every error comes back as ProblemDetails (type, title, status, detail, instance) with a traceId
3. Mapping:
   - ValidationException -> 400
   - NotFoundException -> 404
   - InsufficientStockException -> 409, with the failing items as an extension
   - ConcurrencyConflictException -> 409
   - Anything unhandled -> 500 with a generic message, no stack trace outside Development
4. Model validation errors should have the same shape as everything else

CONCURRENCY
Two orders hitting the last unit of stock at the same time must not oversell.
1. Inside the transaction, deduct stock per product with a conditional update using ExecuteUpdateAsync:
   UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE Id = @id AND StockQuantity >= @qty
2. If 0 rows are affected, roll back and throw InsufficientStockException
3. Update products in ascending ProductId order so two orders don't deadlock each other
4. Keep RowVersion as a backup. On DbUpdateConcurrencyException retry up to 3 times, then return 409
5. EnableRetryOnFailure is on, so the manual transaction must be wrapped in strategy.ExecuteAsync, otherwise it throws at runtime
6. The CHECK constraint on StockQuantity is the last line of defence at DB level

TESTS
Run tests against my local SQL Server, not EF InMemory. InMemory doesn't support transactions or rowversion so it can't prove concurrency. Use a separate test database (OrderDb_Tests) so the dev database is never touched. Before each test class, drop and recreate it with migrations and seed data (EnsureDeleted + Migrate). Read the test connection string from appsettings or an environment variable so it's easy to change on another machine.

1. Insufficient stock -> 409, stock unchanged, no order created
2. Quantity 0 and negative -> 400
3. Total is correct for multiple items, and UnitPrice on the order stays the same after the product price is changed later
4. Product with stock 1, send 10 POST requests in parallel with Task.WhenAll -> exactly one 201, the rest 409, final stock 0
5. Order id that doesn't exist -> 404 ProblemDetails
6. Pagination returns the correct totalCount
7. Report returns at most 5 rows, sorted by revenue

DELIVERABLES
1. No Docker. The app runs against a locally installed SQL Server.
2. Connection strings in appsettings.Development.json using Windows authentication (default SQL Server instance):
   App:   Server=localhost;Database=OrderDb;Trusted_Connection=True;TrustServerCertificate=True
   Tests: Server=localhost;Database=OrderDb_Tests;Trusted_Connection=True;TrustServerCertificate=True
   In the README, mention that if SQL Server runs as a named instance (e.g. .\SQLEXPRESS) or LocalDB, only the Server part needs to change.
3. README.md with:
   - prerequisites (.NET 8 SDK, SQL Server any edition including Express or LocalDB, dotnet-ef tool)
   - exact setup commands (dotnet tool install --global dotnet-ef, dotnet ef database update with the correct --project and --startup-project) and how seeding works
   - how to run the API and open Swagger
   - how to run tests
   - curl examples for every endpoint
   - design decisions: status codes, concurrency approach, report query with the generated SQL, date filter behaviour
   - limitations and what could be improved later
4. PROMPTS.md: a short log of each step, what was done, and any issue that came up and how it got fixed

HOW WE'LL WORK
1. Build it in this order and commit after each step:
   a. solution setup, entities, DbContext, migration, seed
   b. order endpoints
   c. report endpoint
   d. error handling
   e. concurrency
   f. tests
   g. README and PROMPTS.md
2. Run dotnet build after every step, and dotnet test once tests exist. Don't tell me something works unless you've actually run it.
3. Async everywhere, pass CancellationToken through.
4. Comments only where the logic isn't obvious.

FOR NOW
Reply with only:
1. Solution and folder structure
2. Entities with their main fields
3. Each endpoint with its status codes
4. Your concurrency approach in 3-4 lines
5. Any assumptions or questions you have
Then stop and wait for "go".
```

</details>

**What came back:** a plan with the folder structure, entities, a status code table for each endpoint, a concurrency summary and 7 assumptions. Structure, status codes and the core concurrency idea (conditional UPDATE, ascending Id order, `strategy.ExecuteAsync`) were correct.

**What I rejected or changed:**

1. **Seed orders with fixed dates in HasData.** HasData needs static values, so the AI planned fixed dates. That means after a few weeks every seeded order falls outside the "last 30 days" window and the report returns nothing. I moved past orders to a runtime seeder that uses dates relative to `DateTime.UtcNow`.
2. **"to" date as 23:59:59.999.** `datetime2` is more precise than that, so orders in the last fraction of a second would be missed. I changed it to an exclusive upper bound: `OrderDate < to.Date.AddDays(1)`.
3. **Concurrency logic in the wrong layer.** The plan exposed `Database` on `IAppDbContext`, which meant the transaction would be written in the Application layer. I had it moved to an `OrderPlacementService` in Infrastructure behind an interface, and removed `Database` from `IAppDbContext`.
4. **Missing "available" stock in the 409.** A failed conditional UPDATE only tells you it failed, not how much stock is left. I added a read of current stock inside the transaction so the error can show requested vs available.
5. **RowVersion overstated.** The plan said RowVersion conflicts would be retried, but `ExecuteUpdateAsync` doesn't check rowversion, so that path will practically never fire. I kept RowVersion but asked for the README to be honest that the real guarantee is the conditional UPDATE plus the CHECK constraint.

Accepted as-is: merging duplicate productIds, server-assigned UTC OrderDate, UnitPrice snapshot inside the transaction, no auth.

---

## Prompt 2: Plan corrections and step (a)

<details>
<summary>Full prompt</summary>

```text
Plan looks good overall. A few changes before you start:

1. Seed data: keep Customers and Products in HasData. Don't put past orders in HasData with fixed dates, they'll fall outside the "last 30 days" window after a few weeks and the report will be empty. Instead add a DbSeeder class in Infrastructure that runs on startup in Development only, checks if Orders is empty, and creates around 25-30 orders spread over the last 60 days relative to DateTime.UtcNow. It must also reduce product stock for those orders so data stays consistent. Tests should create their own orders and not depend on this seeder.

2. Date filter: don't use 23:59:59.999, datetime2 has more precision than that. If "to" is date-only, filter with OrderDate < to.Date.AddDays(1). "from" stays inclusive (OrderDate >= from).

3. Concurrency placement: the transaction and stock deduction should live in Infrastructure as agreed. Add an interface in Application, something like IOrderPlacementService with PlaceOrderAsync(customerId, mergedItems, ct), and implement it in Infrastructure/Services/OrderPlacementService.cs. OrderService in Application does validation (empty items, quantity, merge duplicates, customer and products exist) and then calls it. Remove Database from IAppDbContext, Application shouldn't need it.
   Flow inside PlaceOrderAsync:
   - strategy.ExecuteAsync -> begin transaction
   - read current price and stock for all requested products
   - if any has insufficient stock, throw InsufficientStockException with productId, requested, available for every failing item
   - run the conditional ExecuteUpdateAsync per product in ascending Id order; if any returns 0 rows (someone else took the stock in between), re-read the stock for that product, throw InsufficientStockException, transaction rolls back
   - create the Order and OrderItems with UnitPrice from the price read in this transaction, SaveChanges, commit

4. RowVersion: keep it, but be honest about it. ExecuteUpdateAsync doesn't check rowversion, so the real guarantee is the conditional UPDATE plus the CHECK constraint. Write that clearly in the README and in a short comment in OrderPlacementService.

5. TotalAmount and LineTotal are normal stored columns set by the domain (Order/OrderItem), not SQL computed columns.

Now do step (a) only:
- create the solution and the 5 projects with the right references
- entities, configurations, AppDbContext, IAppDbContext
- DependencyInjection.cs with EnableRetryOnFailure
- connection strings in appsettings.Development.json
- .gitignore for .NET
- initial migration with Customers and Products seed
- DbSeeder for past orders (Development only)
- run dotnet build and dotnet ef database update, confirm the tables and seed rows exist
- commit with a clear message

Stop after step (a), tell me what you ran and the result, and wait.
```

</details>

**What came back:** solution and 5 projects with references, EF Core 8.0.10 packages, clean build, `InitialCreate` migration with CHECK constraints, indexes, rowversion and seed for 10 customers and 20 products. Database created and verified with sqlcmd. DbSeeder inserted 28 orders and 37 order items over the last 60 days with stock deducted. Committed as `f8f32cc` and pushed.

**What I caught:** Claude Code mentioned that the **.NET 9 SDK** created an `OrderApi.slnx` file instead of a classic `.sln`. The assessment stack is .NET 8. A `.slnx` needs newer tooling (.NET SDK 9.0.200+ / VS 17.13+), so a reviewer on .NET 8 might not be able to open it. A .NET 9 SDK also defaults new projects to `net9.0`, so I needed to confirm the projects actually target .NET 8. Fixed in Prompt 3.

---

## Prompt 3: .NET 8 fixes and step (b) order endpoints

<details>
<summary>Full prompt</summary>

```text
Before step (b), fix two things from step (a):

1. Target framework: the assessment stack is .NET 8. Check every .csproj and make sure TargetFramework is net8.0, not net9.0. If any were created as net9.0, change them, and make sure all Microsoft.* and EF Core packages are 8.x versions. Add a global.json at the root pinning the SDK to 8.0.x with rollForward latestFeature, so anyone with the .NET 8 SDK can build it. If I don't have an 8.0 SDK installed, tell me instead of silently using 9.

2. Solution file: replace OrderApi.slnx with a classic OrderApi.sln (dotnet new sln --format sln, or dotnet new sln if that flag isn't supported) and add all 5 projects to it. .slnx needs newer tooling and a reviewer on .NET 8 or older Visual Studio won't be able to open it. Delete the .slnx.

3. Make sure DbSeeder only runs when the environment is Development, so it never runs in tests (tests will use a "Testing" environment).

Run dotnet build and dotnet ef database update again to confirm nothing broke, then commit.

Then do step (b), the order endpoints:

- POST /api/orders, GET /api/orders/{id}, GET /api/orders, exactly as described in the requirements
- OrderService in Application handles validation: empty items, quantity <= 0 (say which item index), merge duplicate productIds, customer exists, all products exist (list missing ids)
- OrderPlacementService in Infrastructure handles the transaction and stock deduction with the flow we agreed in the previous message (strategy.ExecuteAsync, read stock, conditional ExecuteUpdateAsync in ascending Id order, UnitPrice from the price read inside the transaction)
- Request/query validation for GET /api/orders: page >= 1, pageSize 1-100, from <= to, date filter with OrderDate < to.Date.AddDays(1) when "to" is date-only
- Use AsNoTracking and Select projections for the GET endpoints
- 201 Created with a Location header from POST
- Keep controllers thin

For now it's fine if domain exceptions come back as 500, global error handling is step (d).

When done:
- dotnet build
- run the API and actually call each endpoint with curl: one successful order, one with quantity 0, one with more quantity than in stock, get by id, get a missing id, list with customerId + page + pageSize, list with from/to
- show me the commands and the response status codes
- confirm stock was reduced in the DB after the successful order and unchanged after the failed one
- commit

Stop after step (b) and wait.
```

</details>

**What came back:**

- Pre-step fixes committed as `395db45`: `global.json` pins the SDK to 8.0.400 (`dotnet --version` confirmed 8.0.400), classic `OrderApi.sln` created with all 5 projects, `.slnx` deleted. DbSeeder was already gated by `IsDevelopment()`, so no change was needed there.
- Step (b) committed as `149ca4c`. Curl results:
  - POST valid order: 201, Location `/api/orders/29`, total 2689.95 correct
  - POST quantity 0 / -1: 500 (ValidationException, becomes 400 after error handling)
  - POST more than stock (product 18, qty 5, stock 1): 500 (InsufficientStockException, becomes 409 after error handling)
  - GET `/api/orders/29`: 200 with customer name, items, unit price, line total
  - GET `/api/orders/99999`: 500 (NotFoundException, becomes 404 after error handling)
  - GET with customerId + paging: 200, totalCount 4
  - GET with from/to: 200, date filter working
- Stock check in the DB: product 1 went 48 → 46, product 2 went 188 → 185, product 18 stayed at 1 after the rejected order.
- Claude Code also found and fixed a bug in its own seeder from step (a): `rng.Next(-12, 12)` could give some seeded orders a future OrderDate. Changed to `rng.Next(-8, 0)`.

**What I changed or rejected:**

- The 500s were expected at this point, since global error handling was planned for a later step.
- Several validation paths were not exercised (empty items, unknown customer, unknown product, duplicate productId merge, invalid paging, from after to). I added them to the verification list for the error handling step.
- To save time I merged the report and error handling steps into one prompt. The concurrency logic was already built in step (b) inside `OrderPlacementService`, so the separate concurrency step becomes the parallel-orders test in the tests step.

---

## Prompt 4: Step (c) report and step (d) global error handling

<details>
<summary>Full prompt</summary>

```text
Good work on step (b). Next, do steps (c) and (d) together.

STEP (c) - REPORT
- GET /api/reports/top-products?days=30, days default 30, range 1-365
- Filter orders with OrderDate >= DateTime.UtcNow.AddDays(-days)
- Group OrderItems by product, return productId, productName, unitsSold (SUM Quantity), revenue (SUM LineTotal)
- Order by revenue desc, then unitsSold desc, then productId, Take(5)
- Must run as one query in the database. No ToList/AsEnumerable before the GroupBy.
- Turn on EF command logging in Development, run the endpoint once, and copy the actual generated SQL into a scratch note for the README later. Confirm it has GROUP BY, ORDER BY and TOP(5) in it.
- ReportService in Application, thin ReportsController in Api

STEP (d) - GLOBAL ERROR HANDLING
- GlobalExceptionHandler implementing IExceptionHandler, registered with AddExceptionHandler and AddProblemDetails, app.UseExceptionHandler()
- Mapping:
  - ValidationException -> 400 (ValidationProblemDetails with an errors dictionary, e.g. "items[1].quantity")
  - NotFoundException -> 404
  - InsufficientStockException -> 409 with a "failingItems" extension (productId, requested, available)
  - ConcurrencyConflictException -> 409
  - anything else -> 500, generic detail, exception message only in Development
- Every response has type, title, status, detail, instance and a traceId extension
- Configure InvalidModelStateResponseFactory so model binding errors (bad JSON, wrong types, invalid date format) come back in the same shape with traceId
- Use AddProblemDetails CustomizeProblemDetails to add traceId in one place instead of repeating it

VERIFY WITH CURL (show commands and status codes):
- report with days=30 -> 200, max 5 rows
- report with days=0 and days=400 -> 400
- POST quantity 0 -> 400 with errors dictionary
- POST empty items -> 400
- POST unknown customerId -> 400
- POST unknown productId -> 400 listing the missing ids
- POST same productId twice -> 201 and one merged order item
- POST more than stock -> 409 with failingItems
- POST malformed JSON -> 400 ProblemDetails
- GET /api/orders/99999 -> 404
- GET /api/orders?page=0, pageSize=500, from after to -> 400 each

Run dotnet build, commit and push. Stop after this and wait.
```

</details>

**What came back:** pushed as `f8f32cc..f6bb60f`.

- Report: the generated SQL was checked and the whole aggregation runs in the database:

```sql
SELECT TOP(@__p_1) [o].[ProductId], [p].[Name] AS [ProductName],
       COALESCE(SUM([o].[Quantity]), 0) AS [UnitsSold],
       COALESCE(SUM([o].[LineTotal]), 0.0) AS [Revenue]
FROM [OrderItems] AS [o]
INNER JOIN [Orders] AS [o0] ON [o].[OrderId] = [o0].[Id]
INNER JOIN [Products] AS [p] ON [o].[ProductId] = [p].[Id]
WHERE [o0].[OrderDate] >= @__cutoff_0
GROUP BY [o].[ProductId], [p].[Name]
ORDER BY COALESCE(SUM([o].[LineTotal]), 0.0) DESC,
         COALESCE(SUM([o].[Quantity]), 0) DESC, [o].[ProductId]
```

- Error handling: `GlobalExceptionHandler` (IExceptionHandler), `InvalidModelStateResponseFactory` and `CustomizeProblemDetails` for traceId. Claude Code reported all 14 curl checks returning the expected status codes, and every error has type, title, status, detail, instance and traceId.

**What I noticed:**

- Claude Code said `ReportService` "was already written". It had built the report during step (b) even though that step was only the order endpoints. No harm done, but it went outside the scope I gave it.
- This time it summarised the curl results as "all 14 passed" instead of showing each one. I accepted it because the tests in the next step check the same behaviour with real HTTP calls.

---

## Prompt 5: Step (f) tests, including the concurrency proof

<details>
<summary>Full prompt</summary>

```text
Steps (c) and (d) look good. Now step (f), the tests. This also covers step (e), since the concurrency logic was already built in OrderPlacementService and the test below is what proves it.

SETUP
- tests/OrderApi.Tests uses WebApplicationFactory<Program> with environment "Testing"
- Connection string for OrderDb_Tests from tests/OrderApi.Tests/appsettings.json, overridable with an environment variable
- The factory replaces the app's DbContext registration so the API under test points at OrderDb_Tests
- Before each test class: EnsureDeleted + Migrate, so it starts with only the Customers and Products seed from migrations (DbSeeder must not run)
- All test classes share one database, so put them in a single xUnit collection or disable parallel collections (xunit.runner.json) to avoid tests stepping on each other
- Tests create their own orders and set up the stock they need directly through the DbContext, don't rely on specific seeded stock values staying the same
- Add `public partial class Program { }` to the Api project if it's needed for WebApplicationFactory

TESTS
1. Insufficient stock: set a product's stock to 2, order 5 -> 409, response has failingItems with requested 5 and available 2, stock still 2, no order in the DB
2. Invalid quantity: quantity 0 -> 400, quantity -3 -> 400, both with an errors dictionary
3. Total calculation: order 2 products with different quantities -> TotalAmount equals the sum of quantity * price; then change one product's price in the DB and GET the order again -> UnitPrice and TotalAmount on the order are unchanged
4. Concurrency: set a product's stock to 1, send 10 POST requests at the same time with Task.WhenAll -> exactly one 201, nine 409, final stock 0, exactly one order item for that product in the DB
5. Concurrency with partial stock: stock 5, 10 parallel orders of quantity 1 -> exactly five 201, five 409, final stock 0
6. GET /api/orders/{id} for a missing id -> 404 ProblemDetails with traceId
7. Pagination: create 7 orders for one customer, GET with pageSize=3&page=3 -> totalCount 7, totalPages 3, 1 item
8. Report: create orders for 6+ different products with known totals -> returns exactly 5 rows, sorted by revenue desc, top row has the expected revenue and unitsSold

RULES
- Use real HTTP calls through the factory's HttpClient, not service calls directly
- Use clear Arrange / Act / Assert sections and descriptive test names
- Run dotnet test 3 times in a row and show the result each time. The concurrency tests must pass every time, if any run is flaky, find the real cause and fix it, don't add retries or sleeps to the test.

Then commit and push. Stop and wait.
```

</details>

Why I added test 5: the stock-1 test only shows that one request wins. Stock 5 with 10 parallel orders shows the conditional UPDATE lets through exactly as many orders as there is stock, no more and no less. I also asked for 3 runs in a row because concurrency bugs often hide on the first run.

**What came back:** pushed as `f6bb60f..7f425fb`. 9 tests, run 3 times in a row, all passing every time.

- Concurrency confirmed by tests:
  - stock 1, 10 parallel orders: exactly one 201, nine 409, final stock 0, one order item
  - stock 5, 10 parallel orders of quantity 1: exactly five 201, five 409, final stock 0, five order items
- No retries or sleeps were needed in the tests. The conditional `ExecuteUpdateAsync` (`WHERE StockQuantity >= qty`) is what actually prevents overselling.

The tests found two bugs that Claude Code then fixed:

1. **Wrong Content-Type on errors.** The exception handler used `WriteAsJsonAsync`, which always sets `application/json`, so error responses were not sent as `application/problem+json`. The curl checks in step (d) didn't catch this. Fixed by using the overload that takes a content type.
2. **Tests affecting each other.** The total calculation test changes product 3's price to 999.00 to check that the order keeps the old unit price. The test setup reset stock but not price, so product 3 then jumped up the revenue ranking in the report test. Fixed by also resetting prices in the test setup of both test classes.

**What I noticed:** both bugs came from earlier AI output (step (d) and the test setup) and were only found because the tests ran against a real database and checked exact values. This is the main reason I insisted on real SQL Server tests instead of EF InMemory.

---

---

## Prompt 6: README

<details>
<summary>Full prompt</summary>

```text
Last step: write the README.md. Keep it short and only cover setup and running, nothing extra.

Include:
1. Prerequisites: .NET 8 SDK, SQL Server (any edition, Express or LocalDB also fine), dotnet-ef tool
2. Connection string: where it is (appsettings.Development.json for the app, tests/OrderApi.Tests/appsettings.json for tests), that it uses localhost with Windows authentication, and that for a named instance like .\SQLEXPRESS or LocalDB only the Server part needs to change
3. Create and seed the database: the exact commands (install dotnet-ef, dotnet ef database update with the right --project and --startup-project). Explain in 2-3 lines that customers and products are seeded by the migration, and sample orders are added by the seeder on the first run in Development
4. Run the API: the exact command and the Swagger URL
5. Run the tests: the exact command, and that tests use a separate OrderDb_Tests database that is recreated automatically

Rules:
- Don't add design decisions, curl examples or future improvements
- Don't create, edit or overwrite PROMPTS.md, I maintain that file myself
- Before finishing, run every command in the README yourself from a clean state (drop OrderDb first) and confirm they work as written

Commit and push.
```

</details>

I kept the README to what the assessment asks for: setup, run instructions and how to create and seed the database. Design decisions and improvements are covered in the debrief instead. I also told Claude Code not to touch this file, since the first prompt had asked it to create a PROMPTS.md and it would have overwritten mine.

**What came back:** README with prerequisites, where the connection strings are, the migration command, how seeding works, the run command with the Swagger URL (`http://localhost:5163/swagger`) and the test command. Claude Code dropped `OrderDb` and ran every README command from a clean state before committing, and left this file untouched.

---

## Things the AI got wrong (running list)

1. Planned to seed past orders with fixed dates, which would leave the report empty after a few weeks.
2. Planned a `to` date filter of 23:59:59.999, which misses rows with `datetime2` precision.
3. Put transaction and concurrency logic in the Application layer by exposing `Database` on `IAppDbContext`.
4. Claimed RowVersion retries would protect stock, but `ExecuteUpdateAsync` doesn't check rowversion.
5. Used the .NET 9 SDK and created a `.slnx` solution even though the stack is .NET 8.
6. The seeder from step (a) could create orders with a future OrderDate (`rng.Next(-12, 12)`). Claude Code caught this itself during step (b) and fixed it.
7. Built the report endpoint during step (b), outside the scope of that step.
8. The global error handler sent ProblemDetails with `application/json` instead of `application/problem+json`. Found by the tests.
9. Test setup only reset stock, not prices, so one test's price change broke the report test. Found on the test runs and fixed.

## Decisions I made myself

- Lightweight Clean Architecture without MediatR, CQRS, AutoMapper or repositories.
- Local SQL Server with Windows authentication instead of Docker.
- Integration tests against a real SQL Server database instead of EF InMemory, because InMemory can't prove transactions or concurrency.
- Step-by-step workflow with a commit per step so the process is visible in the Git history.
