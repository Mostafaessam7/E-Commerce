using Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("StockItems");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ProductVariantId).IsRequired();
        builder.HasIndex(s => s.ProductVariantId).IsUnique();

        // Optimistic concurrency token (ADR-006): a persistence-only shadow property, not a
        // Domain model member — see StockItem's doc comment for why this is what actually
        // prevents two concurrent Reserve() calls from both overselling the last unit.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasMany(s => s.Transactions)
            .WithOne()
            .HasForeignKey("StockItemId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> builder)
    {
        builder.ToTable("StockTransactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.Reason).HasMaxLength(500);
        builder.HasIndex(t => t.OccurredAtUtc);
    }
}
