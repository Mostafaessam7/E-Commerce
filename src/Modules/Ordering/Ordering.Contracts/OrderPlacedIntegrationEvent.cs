using EventBus;

namespace Ordering.Contracts;

/// <summary>Published (via the Outbox — see docs/events.md) once an order is successfully placed.
/// Payments will react to this once it exists (Phase 9); Notifications for the order-confirmation
/// email (Phase 22).</summary>
public sealed record OrderPlacedIntegrationEvent(
    Guid OrderId,
    string OrderNumber,
    Guid? CustomerId,
    string Email,
    decimal Total,
    string Currency) : IntegrationEvent;
