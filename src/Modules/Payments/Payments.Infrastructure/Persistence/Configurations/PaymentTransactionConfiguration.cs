using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payments.Domain;

namespace Payments.Infrastructure.Persistence.Configurations;

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Provider).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ProviderIntentId).HasMaxLength(200).IsRequired();
        builder.Property(p => p.ProviderTransactionId).HasMaxLength(200);
        builder.Property(p => p.FailureReason).HasMaxLength(1000);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.OwnsOne(p => p.Amount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("Currency").HasMaxLength(3);
        });
        builder.OwnsOne(p => p.RefundedAmount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("RefundedAmount").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("RefundedAmountCurrency").HasMaxLength(3);
        });

        builder.HasMany(p => p.Refunds)
            .WithOne()
            .HasForeignKey("PaymentTransactionId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => p.ProviderIntentId);
    }
}

public sealed class RefundTransactionConfiguration : IEntityTypeConfiguration<RefundTransaction>
{
    public void Configure(EntityTypeBuilder<RefundTransaction> builder)
    {
        builder.ToTable("RefundTransactions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason).HasMaxLength(1000);

        builder.OwnsOne(r => r.Amount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("Currency").HasMaxLength(3);
        });
    }
}

public sealed class ProcessedWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        builder.ToTable("ProcessedWebhookEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Provider).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ProviderEventId).HasMaxLength(200).IsRequired();

        builder.HasIndex(e => new { e.Provider, e.ProviderEventId }).IsUnique();
    }
}
