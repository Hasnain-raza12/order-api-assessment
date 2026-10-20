using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderApi.Domain.Entities;

namespace OrderApi.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(p => p.Sku)
            .IsUnique();

        builder.Property(p => p.Price)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.StockQuantity)
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint("CK_Products_StockQuantity", "[StockQuantity] >= 0"));

        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        builder.HasData(
            new Product { Id = 1,  Name = "Laptop Pro 15",        Sku = "LAP-001", Price = 1299.99m, StockQuantity = 50 },
            new Product { Id = 2,  Name = "Wireless Mouse",        Sku = "MOU-001", Price = 29.99m,  StockQuantity = 200 },
            new Product { Id = 3,  Name = "Mechanical Keyboard",   Sku = "KEY-001", Price = 89.99m,  StockQuantity = 75 },
            new Product { Id = 4,  Name = "27-inch Monitor",       Sku = "MON-001", Price = 349.99m, StockQuantity = 30 },
            new Product { Id = 5,  Name = "USB-C Hub",             Sku = "HUB-001", Price = 49.99m,  StockQuantity = 150 },
            new Product { Id = 6,  Name = "Webcam HD 1080p",       Sku = "CAM-001", Price = 69.99m,  StockQuantity = 60 },
            new Product { Id = 7,  Name = "Noise-Cancelling Headphones", Sku = "HDP-001", Price = 199.99m, StockQuantity = 40 },
            new Product { Id = 8,  Name = "External SSD 1TB",      Sku = "SSD-001", Price = 119.99m, StockQuantity = 80 },
            new Product { Id = 9,  Name = "Desk Lamp LED",         Sku = "LMP-001", Price = 34.99m,  StockQuantity = 100 },
            new Product { Id = 10, Name = "Ergonomic Chair",       Sku = "CHR-001", Price = 499.99m, StockQuantity = 15 },
            new Product { Id = 11, Name = "Standing Desk",         Sku = "DSK-001", Price = 699.99m, StockQuantity = 10 },
            new Product { Id = 12, Name = "HDMI Cable 2m",         Sku = "CBL-001", Price = 12.99m,  StockQuantity = 300 },
            new Product { Id = 13, Name = "Cable Management Kit",  Sku = "CMK-001", Price = 19.99m,  StockQuantity = 120 },
            new Product { Id = 14, Name = "Laptop Stand",          Sku = "STD-001", Price = 39.99m,  StockQuantity = 90 },
            new Product { Id = 15, Name = "Mousepad XL",           Sku = "PAD-001", Price = 24.99m,  StockQuantity = 180 },
            new Product { Id = 16, Name = "Smart Power Strip",     Sku = "PWR-001", Price = 44.99m,  StockQuantity = 55 },
            new Product { Id = 17, Name = "Portable Charger 20000mAh", Sku = "CHG-001", Price = 54.99m, StockQuantity = 70 },
            new Product { Id = 18, Name = "Bluetooth Speaker",     Sku = "SPK-001", Price = 79.99m,  StockQuantity = 1 },
            new Product { Id = 19, Name = "Desk Organiser",        Sku = "ORG-001", Price = 27.99m,  StockQuantity = 0 },
            new Product { Id = 20, Name = "Mini Projector",        Sku = "PRJ-001", Price = 249.99m, StockQuantity = 1 }
        );
    }
}
