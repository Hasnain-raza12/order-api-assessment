using OrderApi.Domain.Entities;

namespace OrderApi.Application.Interfaces;

public interface IOrderPlacementService
{
    /// <summary>
    /// Executes the transaction: validates stock, deducts it, persists the order.
    /// Receives already-merged, already-validated items.
    /// </summary>
    Task<Order> PlaceOrderAsync(int customerId, IReadOnlyList<OrderItemInput> items, CancellationToken ct = default);
}

public record OrderItemInput(int ProductId, int Quantity);
