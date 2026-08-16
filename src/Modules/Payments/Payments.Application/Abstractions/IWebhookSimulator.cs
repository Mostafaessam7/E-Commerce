namespace Payments.Application.Abstractions;

/// <summary>
/// Dev/demo-only capability: builds a properly-signed webhook payload the same way the real
/// provider behind <see cref="IPaymentGateway"/> would, so a "Simulate payment" button in
/// Store.Web can drive the real webhook endpoint (real signature verification, real idempotency
/// path) without an actual external provider account. Not part of Section 9's provider contract
/// itself — nothing but the fake implementation should ever implement this.
/// </summary>
public interface IWebhookSimulator
{
    (string Payload, string Signature) BuildSucceededPayload(Guid paymentTransactionId);

    (string Payload, string Signature) BuildFailedPayload(Guid paymentTransactionId, string reason);
}
