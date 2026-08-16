using Catalog.Domain;
using Catalog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(300).IsRequired();
        builder.Property(p => p.ShortDescription).HasMaxLength(500);

        builder.Property(p => p.Slug)
            .HasConversion(slug => slug.Value, value => Slug.Create(value).Value)
            .HasMaxLength(300)
            .IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();

        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.OwnsOne(p => p.Seo, seo =>
        {
            seo.Property(s => s.MetaTitle).HasColumnName("SeoMetaTitle").HasMaxLength(300);
            seo.Property(s => s.MetaDescription).HasColumnName("SeoMetaDescription").HasMaxLength(500);
            seo.Property(s => s.CanonicalUrl).HasColumnName("SeoCanonicalUrl").HasMaxLength(500);
        });

        // Simple many-of-Guid/string relationships (categories, tags, related/cross-sell/upsell
        // product ids) are stored as JSON-backed primitive collections rather than join tables —
        // a deliberate Phase 4 simplification given their low cardinality; revisit with real join
        // tables only if a concrete query need (e.g. "products in category X") demands it.
        builder.PrimitiveCollection(p => p.CategoryIds).HasColumnName("CategoryIds");
        builder.PrimitiveCollection(p => p.Tags).HasColumnName("Tags");
        builder.PrimitiveCollection(p => p.RelatedProductIds).HasColumnName("RelatedProductIds");
        builder.PrimitiveCollection(p => p.CrossSellProductIds).HasColumnName("CrossSellProductIds");
        builder.PrimitiveCollection(p => p.UpsellProductIds).HasColumnName("UpsellProductIds");

        builder.HasMany(p => p.Variants)
            .WithOne()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.BrandId);
        builder.HasIndex(p => p.Status);
    }
}
