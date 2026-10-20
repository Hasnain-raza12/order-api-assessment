using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderApi.Domain.Entities;

namespace OrderApi.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Email)
            .IsRequired()
            .HasMaxLength(300);

        builder.HasIndex(c => c.Email)
            .IsUnique();

        builder.HasData(
            new Customer { Id = 1, Name = "Alice Johnson", Email = "alice@example.com" },
            new Customer { Id = 2, Name = "Bob Smith", Email = "bob@example.com" },
            new Customer { Id = 3, Name = "Carol White", Email = "carol@example.com" },
            new Customer { Id = 4, Name = "David Brown", Email = "david@example.com" },
            new Customer { Id = 5, Name = "Eve Davis", Email = "eve@example.com" },
            new Customer { Id = 6, Name = "Frank Miller", Email = "frank@example.com" },
            new Customer { Id = 7, Name = "Grace Wilson", Email = "grace@example.com" },
            new Customer { Id = 8, Name = "Henry Moore", Email = "henry@example.com" },
            new Customer { Id = 9, Name = "Isla Taylor", Email = "isla@example.com" },
            new Customer { Id = 10, Name = "Jack Anderson", Email = "jack@example.com" }
        );
    }
}
