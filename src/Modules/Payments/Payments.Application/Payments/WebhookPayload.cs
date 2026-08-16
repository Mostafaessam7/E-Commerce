namespace Payments.Application.Payments;

/// <summary>Shape of the JSON body a payment webhook delivers. Every real provider's schema
/// differs; <c>FakePaymentGateway</c> emits exactly this one, and <see cref="ProcessWebhookCommandHandler"/>
/// is written against it — a real provider integration would translate that provider's payload
/// into this same shape (or this record would move per-provider) rather than every consumer
/// learning a provider-specific format.</summary>
public sealed record WebhookPayload(
    string EventId,
    string EventType,
    Guid PaymentTransactionId,
    string? ProviderTransactionId,
    string? FailureReason);
