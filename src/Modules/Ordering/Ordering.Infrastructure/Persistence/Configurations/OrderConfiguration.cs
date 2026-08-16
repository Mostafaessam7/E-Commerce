using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain;
using Ordering.Domain.ValueObjects;

namespace Ordering.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(o => o.OrderNumber).IsUnique();

        builder.Property(o => o.Notes).HasMaxLength(2000);
        builder.Property(o => o.TrackingNumber).HasMaxLength(100);

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.FulfillmentStatus).HasConversion<string>().HasMaxLength(20);

        ConfigureAddress(builder.OwnsOne(o => o.BillingAddress), "Billing");
        ConfigureAddress(builder.OwnsOne(o => o.ShippingAddress), "Shipping");

        builder.OwnsOne(o => o.ShippingCost, m =>
        {
            m.Property(x => x.Amount).HasColumnName("ShippingCost").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("ShippingCostCurrency").HasMaxLength(3);
        });
        builder.OwnsOne(o => o.Tax, m =>
        {
            m.Property(x => x.Amount).HasColumnName("Tax").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("TaxCurrency").HasMaxLength(3);
        });
        builder.OwnsOne(o => o.Discount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("Discount").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("DiscountCurrency").HasMaxLength(3);
        });

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.StatusHistory)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.Status);
    }

    private static void ConfigureAddress(OwnedNavigationBuilder<Order, Address> owned, string prefix)
    {
        owned.Property(a => a.FullName).HasColumnName($"{prefix}FullName").HasMaxLength(200);
        owned.Property(a => a.Phone).HasColumnName($"{prefix}Phone").HasMaxLength(30);
        owned.Property(a => a.Line1).HasColumnName($"{prefix}Line1").HasMaxLength(300);
        owned.Property(a => a.Line2).HasColumnName($"{prefix}Line2").HasMaxLength(300);
        owned.Property(a => a.City).HasColumnName($"{prefix}City").HasMaxLength(150);
        owned.Property(a => a.State).HasColumnName($"{prefix}State").HasMaxLength(150);
        owned.Property(a => a.PostalCode).HasColumnName($"{prefix}PostalCode").HasMaxLength(20);
        owned.Property(a => a.Country).HasColumnName($"{prefix}Country").HasMaxLength(100);
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductName).HasMaxLength(300).IsRequired();
        builder.Property(i => i.Sku).HasMaxLength(100).IsRequired();

        builder.OwnsOne(i => i.UnitPrice, m =>
        {
            m.Property(x => x.Amount).HasColumnName("UnitPrice").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("Currency").HasMaxLength(3);
        });
        builder.OwnsOne(i => i.LineTotal, m =>
        {
            m.Property(x => x.Amount).HasColumnName("LineTotal").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("LineTotalCurrency").HasMaxLength(3);
        });
    }
}

public sealed class OrderStatusHistoryEntryConfiguration : IEntityTypeConfiguration<OrderStatusHistoryEntry>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistoryEntry> builder)
    {
        builder.ToTable("OrderStatusHistory");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Note).HasMaxLength(1000);
    }
}
