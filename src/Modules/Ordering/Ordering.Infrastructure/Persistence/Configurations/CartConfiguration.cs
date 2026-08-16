using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain;

namespace Ordering.Infrastructure.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CouponCode).HasMaxLength(50);

        builder.HasIndex(c => c.CustomerId).IsUnique().HasFilter("[CustomerId] IS NOT NULL");
        builder.HasIndex(c => c.AnonymousId).IsUnique().HasFilter("[AnonymousId] IS NOT NULL");

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey("CartId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductName).HasMaxLength(300).IsRequired();
        builder.Property(i => i.Sku).HasMaxLength(100).IsRequired();

        builder.OwnsOne(i => i.UnitPrice, price =>
        {
            price.Property(m => m.Amount).HasColumnName("UnitPrice").HasColumnType("decimal(18,2)");
            price.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
        });
    }
}
