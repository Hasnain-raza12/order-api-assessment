using Microsoft.AspNetCore.Mvc;
using OrderApi.Application.Interfaces;

namespace OrderApi.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> GetTopProducts(
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        var result = await _reportService.GetTopProductsAsync(days, ct);
        return Ok(result);
    }
}
