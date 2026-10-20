using OrderApi.Application.DTOs;

namespace OrderApi.Application.Interfaces;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<OrderResponse> GetOrderAsync(int id, CancellationToken ct = default);
    Task<PagedResponse<OrderSummaryResponse>> GetOrdersAsync(OrdersQueryRequest query, CancellationToken ct = default);
}
