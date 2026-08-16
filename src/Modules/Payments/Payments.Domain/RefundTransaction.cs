using SharedKernel.Primitives;
using SharedKernel.ValueObjects;

namespace Payments.Domain;

/// <summary>One refund (full or partial) against a <see cref="PaymentTransaction"/> — Section 9's
/// "Refund, Partial Refund". A single payment can have several partial refunds over time.</summary>
public sealed class RefundTransaction : Entity<Guid>
{
    internal RefundTransaction(Guid id, Money amount, string? reason, DateTime processedAtUtc)
        : base(id)
    {
        Amount = amount;
        Reason = reason;
        ProcessedAtUtc = processedAtUtc;
    }

    private RefundTransaction()
    {
    }

    public Money Amount { get; private set; } = null!;

    public string? Reason { get; private set; }

    public DateTime ProcessedAtUtc { get; private set; }
}
