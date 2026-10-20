namespace OrderApi.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public void CalculateTotal()
    {
        TotalAmount = Items.Sum(i => i.LineTotal);
    }
}
