using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shipping.Domain;

namespace Shipping.Infrastructure.Persistence.Configurations;

public sealed class ShippingMethodConfiguration : IEntityTypeConfiguration<ShippingMethod>
{
    public void Configure(EntityTypeBuilder<ShippingMethod> builder)
    {
        builder.ToTable("ShippingMethods");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(500);

        builder.OwnsOne(m => m.Cost, cost =>
        {
            cost.Property(x => x.Amount).HasColumnName("Cost").HasColumnType("decimal(18,2)");
            cost.Property(x => x.Currency).HasColumnName("CostCurrency").HasMaxLength(3);
        });
    }
}
