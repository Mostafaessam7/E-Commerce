using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reviews.Domain;

namespace Reviews.Infrastructure.Persistence.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReviewerName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.ReviewerEmail).HasMaxLength(256);
        builder.Property(r => r.Title).HasMaxLength(200);
        builder.Property(r => r.Body).HasMaxLength(4000).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(r => new { r.ProductId, r.Status });
    }
}
