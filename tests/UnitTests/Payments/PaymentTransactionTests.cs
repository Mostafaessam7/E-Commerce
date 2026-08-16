using FluentAssertions;
using Payments.Domain;
using Payments.Domain.Events;

namespace UnitTests.Payments;

public class PaymentTransactionTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PaymentTransaction CreateTransaction(decimal amount = 100m) =>
        PaymentTransaction.Initialize(Guid.NewGuid(), amount, "EGP", "fake", "pi_test123").Value;

    [Fact]
    public void Initialize_starts_Pending()
    {
        var payment = CreateTransaction();

        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.Amount.Amount.Should().Be(100m);
    }

    [Fact]
    public void MarkSucceeded_transitions_to_Succeeded_and_raises_PaymentSucceededDomainEvent()
    {
        var payment = CreateTransaction();

        var result = payment.MarkSucceeded("txn_123", Now);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        payment.ProviderTransactionId.Should().Be("txn_123");
        payment.DomainEvents.Should().ContainSingle(e => e is PaymentSucceededDomainEvent);
    }

    [Fact]
    public void MarkSucceeded_fails_when_already_resolved_defense_in_depth_against_duplicate_webhooks()
    {
        var payment = CreateTransaction();
        payment.MarkSucceeded("txn_123", Now);

        var result = payment.MarkSucceeded("txn_123", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.AlreadyResolved");
    }

    [Fact]
    public void MarkFailed_fails_once_already_succeeded_out_of_order_webhook_protection()
    {
        var payment = CreateTransaction();
        payment.MarkSucceeded("txn_123", Now);

        var result = payment.MarkFailed("card_declined", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.AlreadyResolved");
    }

    [Fact]
    public void Refund_fails_for_a_payment_that_has_not_succeeded_yet()
    {
        var payment = CreateTransaction();

        var result = payment.Refund(50m, "customer request", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.CannotRefund");
    }

    [Fact]
    public void Partial_refund_leaves_status_PartiallyRefunded()
    {
        var payment = CreateTransaction(100m);
        payment.MarkSucceeded("txn_123", Now);

        var result = payment.Refund(40m, "partial return", Now);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        payment.RefundedAmount.Amount.Should().Be(40m);
    }

    [Fact]
    public void Refunding_the_full_amount_sets_status_Refunded_and_raises_full_refund_event()
    {
        var payment = CreateTransaction(100m);
        payment.MarkSucceeded("txn_123", Now);

        var result = payment.Refund(100m, "full return", Now);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        var refundEvent = payment.DomainEvents.OfType<PaymentRefundedDomainEvent>().Should().ContainSingle().Subject;
        refundEvent.IsFullRefund.Should().BeTrue();
    }

    [Fact]
    public void Refund_fails_when_it_would_exceed_the_remaining_refundable_amount()
    {
        var payment = CreateTransaction(100m);
        payment.MarkSucceeded("txn_123", Now);
        payment.Refund(60m, "first", Now);

        var result = payment.Refund(50m, "second", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.RefundExceedsRemaining");
    }
}
