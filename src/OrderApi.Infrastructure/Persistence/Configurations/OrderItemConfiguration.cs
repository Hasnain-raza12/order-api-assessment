using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderApi.Domain.Entities;

namespace OrderApi.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] > 0"));

        builder.Property(i => i.UnitPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(i => i.LineTotal)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ProductId);
    }
}
