using Microsoft.AspNetCore.Mvc;
using OrderApi.Api.Middleware;
using OrderApi.Infrastructure;
using OrderApi.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails(options =>
{
    // Stamp traceId onto every ProblemDetails produced by the framework
    // (middleware-produced ones get it from GlobalExceptionHandler directly)
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        ctx.ProblemDetails.Instance ??= ctx.HttpContext.Request.Path;
    };
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Model binding / validation failures return ValidationProblemDetails with traceId
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ctx =>
    {
        var pd = new ValidationProblemDetails(ctx.ModelState)
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "Validation failed.",
            Status = StatusCodes.Status400BadRequest,
            Instance = ctx.HttpContext.Request.Path
        };
        pd.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(pd) { ContentTypes = { "application/problem+json" } };
    };
});

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Run the startup seeder for past orders in Development only
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedOrdersAsync(db);
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make Program accessible to WebApplicationFactory in tests
public partial class Program { }
