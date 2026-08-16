using Payments.Domain.Events;
using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;
using SharedKernel.ValueObjects;

namespace Payments.Domain;

/// <summary>
/// Aggregate root for one payment attempt against an order (Section 9). Referenced by
/// <see cref="OrderId"/> only — a plain Guid, never a navigation into Ordering's tables (module
/// boundary rules). Status transitions are gated the same way <c>Ordering.Domain.Order</c>'s are:
/// named methods only, each validating the current state first — a webhook delivered twice, out
/// of order, or after the transaction already resolved is rejected here as defense-in-depth on
/// top of the idempotency check in Payments.Infrastructure (dedupe by provider event id).
/// </summary>
public sealed class PaymentTransaction : AggregateRoot<Guid>
{
    private readonly List<RefundTransaction> _refunds = [];

    private PaymentTransaction(Guid id, Guid orderId, Money amount, string provider, string providerIntentId)
        : base(id)
    {
        OrderId = orderId;
        Amount = amount;
        Provider = provider;
        ProviderIntentId = providerIntentId;
        Status = PaymentStatus.Pending;
        RefundedAmount = Money.Zero(amount.Currency);
    }

    private PaymentTransaction()
    {
    }

    public Guid OrderId { get; private set; }

    public Money Amount { get; private set; } = null!;

    public string Provider { get; private set; } = null!;

    public string ProviderIntentId { get; private set; } = null!;

    public string? ProviderTransactionId { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public Money RefundedAmount { get; private set; } = null!;

    public IReadOnlyCollection<RefundTransaction> Refunds => _refunds.AsReadOnly();

    public static Result<PaymentTransaction> Initialize(Guid orderId, decimal amount, string currency, string provider, string providerIntentId)
    {
        Guard.Against.Empty(orderId, nameof(orderId));
        Guard.Against.NullOrWhiteSpace(provider, nameof(provider));
        Guard.Against.NullOrWhiteSpace(providerIntentId, nameof(providerIntentId));

        var amountResult = Money.Create(amount, currency);
        if (amountResult.IsFailure)
        {
            return Result.Failure<PaymentTransaction>(amountResult.Error);
        }

        return Result.Success(new PaymentTransaction(Guid.NewGuid(), orderId, amountResult.Value, provider, providerIntentId));
    }

    public Result MarkSucceeded(string providerTransactionId, DateTime processedAtUtc)
    {
        Guard.Against.NullOrWhiteSpace(providerTransactionId, nameof(providerTransactionId));

        if (Status != PaymentStatus.Pending)
        {
            return Result.Failure(Error.Conflict(
                "Payment.AlreadyResolved", $"This payment is already in status '{Status}' and cannot be marked succeeded again."));
        }

        Status = PaymentStatus.Succeeded;
        ProviderTransactionId = providerTransactionId;
        ProcessedAtUtc = processedAtUtc;
        RaiseDomainEvent(new PaymentSucceededDomainEvent(Id, OrderId));

        return Result.Success();
    }

    public Result MarkFailed(string reason, DateTime processedAtUtc)
    {
        Guard.Against.NullOrWhiteSpace(reason, nameof(reason));

        if (Status != PaymentStatus.Pending)
        {
            return Result.Failure(Error.Conflict(
                "Payment.AlreadyResolved", $"This payment is already in status '{Status}' and cannot be marked failed."));
        }

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        ProcessedAtUtc = processedAtUtc;
        RaiseDomainEvent(new PaymentFailedDomainEvent(Id, OrderId, reason));

        return Result.Success();
    }

    public Result Refund(decimal amount, string? reason, DateTime processedAtUtc)
    {
        if (Status is not (PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded))
        {
            return Result.Failure(Error.Conflict(
                "Payment.CannotRefund", $"Cannot refund a payment in status '{Status}'."));
        }

        var amountResult = Money.Create(amount, Amount.Currency);
        if (amountResult.IsFailure)
        {
            return Result.Failure(amountResult.Error);
        }

        var remaining = Amount.Subtract(RefundedAmount);
        if (amountResult.Value.Amount > remaining.Amount)
        {
            return Result.Failure(Error.Validation(
                "Payment.RefundExceedsRemaining", $"Cannot refund {amountResult.Value.Amount} — only {remaining.Amount} is refundable."));
        }

        _refunds.Add(new RefundTransaction(Guid.NewGuid(), amountResult.Value, reason, processedAtUtc));
        RefundedAmount = RefundedAmount.Add(amountResult.Value);

        var isFullRefund = RefundedAmount.Amount == Amount.Amount;
        Status = isFullRefund ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;

        RaiseDomainEvent(new PaymentRefundedDomainEvent(Id, OrderId, amountResult.Value.Amount, isFullRefund));

        return Result.Success();
    }
}
