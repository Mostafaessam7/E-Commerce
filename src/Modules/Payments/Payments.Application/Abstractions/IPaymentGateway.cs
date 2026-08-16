using SharedKernel.Results;

namespace Payments.Application.Abstractions;

public sealed record PaymentIntentResult(string ProviderIntentId, string? RedirectUrl);

public sealed record RefundResult(string ProviderRefundId);

/// <summary>
/// Section 9's required abstraction — the system depends on this, never on a specific provider's
/// SDK. <see cref="Payments.Infrastructure"/>'s <c>FakePaymentGateway</c> is the only
/// implementation today (no real Stripe/Paymob account exists); adding a real one means adding a
/// new class here, not touching Payments.Application/Domain or any other module.
/// </summary>
public interface IPaymentGateway
{
    string ProviderName { get; }

    Task<Result<PaymentIntentResult>> CreateIntentAsync(Guid orderId, decimal amount, string currency, CancellationToken cancellationToken = default);

    Task<Result<RefundResult>> RefundAsync(string providerTransactionId, decimal amount, string currency, CancellationToken cancellationToken = default);

    /// <summary>Verifies the webhook payload was actually sent by this provider (Section 9:
    /// "يجب التحقق من Webhook Signature") — HMAC or provider-specific signature scheme.</summary>
    bool VerifyWebhookSignature(string rawPayload, string signature);
}
