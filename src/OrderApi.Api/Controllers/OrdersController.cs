using Microsoft.AspNetCore.Mvc;
using OrderApi.Application.DTOs;
using OrderApi.Application.Interfaces;

namespace OrderApi.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        var order = await _orderService.CreateOrderAsync(request, ct);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrder(int id, CancellationToken ct)
    {
        var order = await _orderService.GetOrderAsync(id, ct);
        return Ok(order);
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int? customerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new OrdersQueryRequest
        {
            CustomerId = customerId,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize
        };

        var result = await _orderService.GetOrdersAsync(query, ct);
        return Ok(result);
    }
}
