using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderApi.Domain.Exceptions;

namespace OrderApi.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(IHostEnvironment env)
    {
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail, extensions) = exception switch
        {
            Domain.Exceptions.ValidationException ve => (
                StatusCodes.Status400BadRequest,
                "Validation failed.",
                ve.Message,
                (IDictionary<string, object?>?)BuildValidationExtensions(ve)),

            NotFoundException nfe => (
                StatusCodes.Status404NotFound,
                "Resource not found.",
                nfe.Message,
                (IDictionary<string, object?>?)null),

            InsufficientStockException ise => (
                StatusCodes.Status409Conflict,
                "Insufficient stock.",
                ise.Message,
                (IDictionary<string, object?>?)BuildStockExtensions(ise)),

            ConcurrencyConflictException cce => (
                StatusCodes.Status409Conflict,
                "Concurrency conflict.",
                cce.Message,
                (IDictionary<string, object?>?)null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                _env.IsDevelopment() ? exception.Message : "An internal server error occurred.",
                (IDictionary<string, object?>?)null)
        };

        var traceId = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;

        if (exception is Domain.Exceptions.ValidationException validationEx && validationEx.Errors.Count > 0)
        {
            var pd = new ValidationProblemDetails(validationEx.Errors)
            {
                Type = GetRfcType(status),
                Title = title,
                Status = status,
                Detail = detail,
                Instance = httpContext.Request.Path
            };
            pd.Extensions["traceId"] = traceId;

            await httpContext.Response.WriteAsJsonAsync(pd,
                options: null, contentType: "application/problem+json", cancellationToken);
            return true;
        }

        var problem = new ProblemDetails
        {
            Type = GetRfcType(status),
            Title = title,
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = traceId;

        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
                problem.Extensions[key] = value;
        }

        await httpContext.Response.WriteAsJsonAsync(problem,
            options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }

    private static IDictionary<string, object?> BuildValidationExtensions(Domain.Exceptions.ValidationException ve)
        => new Dictionary<string, object?> { ["errors"] = ve.Errors };

    private static IDictionary<string, object?> BuildStockExtensions(InsufficientStockException ise)
        => new Dictionary<string, object?>
        {
            ["failingItems"] = ise.Shortfalls.Select(s => new
            {
                productId = s.ProductId,
                requested = s.Requested,
                available = s.Available
            })
        };

    private static string GetRfcType(int status) => status switch
    {
        400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        _   => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
    };
}
