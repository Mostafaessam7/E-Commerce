using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Promotions.Domain;

namespace Promotions.Infrastructure.Persistence.Configurations;

public sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("Coupons");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();

        builder.Property(c => c.DiscountType).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Value).HasColumnType("decimal(18,2)");
        builder.Property(c => c.Currency).HasMaxLength(3).IsRequired();
        builder.Property(c => c.MinimumOrderAmount).HasColumnType("decimal(18,2)");
    }
}
