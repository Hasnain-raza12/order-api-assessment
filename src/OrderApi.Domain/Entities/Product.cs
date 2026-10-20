namespace OrderApi.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }

    // Concurrency token — used by EF to detect optimistic conflicts on SaveChanges paths.
    // The real stock-oversell guard is the conditional UPDATE in OrderPlacementService.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
