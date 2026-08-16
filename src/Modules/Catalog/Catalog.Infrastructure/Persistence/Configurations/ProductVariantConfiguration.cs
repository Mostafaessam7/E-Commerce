using Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Persistence.Configurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Sku).HasMaxLength(100).IsRequired();
        builder.HasIndex(v => v.Sku).IsUnique();
        builder.Property(v => v.Barcode).HasMaxLength(100);
        builder.Property(v => v.ImageUrl).HasMaxLength(500);

        builder.OwnsOne(v => v.Price, price =>
        {
            price.Property(m => m.Amount).HasColumnName("Price").HasColumnType("decimal(18,2)");
            price.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
        });

        builder.OwnsOne(v => v.SalePrice, price =>
        {
            price.Property(m => m.Amount).HasColumnName("SalePrice").HasColumnType("decimal(18,2)");
            price.Property(m => m.Currency).HasColumnName("SalePriceCurrency").HasMaxLength(3);
        });

        builder.OwnsMany(v => v.Options, options =>
        {
            options.ToTable("ProductVariantOptions");
            options.WithOwner().HasForeignKey("ProductVariantId");
            options.Property<Guid>("Id").ValueGeneratedOnAdd();
            options.HasKey("Id");
            options.Property(o => o.AttributeId).IsRequired();
            options.Property(o => o.AttributeValueId).IsRequired();
        });
    }
}
