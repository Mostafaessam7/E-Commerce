using EventBus;

namespace Payments.Contracts;

/// <summary>Published (via the Outbox) once a webhook confirms a successful charge. No consumer
/// yet — Notifications (order-confirmation email) will react to this once it exists.</summary>
public sealed record PaymentSucceededIntegrationEvent(Guid PaymentTransactionId, Guid OrderId, decimal Amount, string Currency) : IntegrationEvent;
