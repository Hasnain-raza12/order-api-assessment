namespace OrderApi.Application.DTOs;

public class CreateOrderRequest
{
    public int CustomerId { get; set; }
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
