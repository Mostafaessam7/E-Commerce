namespace EventBus;

/// <summary>
/// A fact one module publishes for other modules to react to — the only sanctioned way modules
/// talk to each other (see the module boundary rules: no module reaches into another module's
/// DbContext or tables directly). Concrete events are versioned, serializable contracts that live
/// in the *publishing* module's <c>*.Contracts</c> project, e.g.
/// <c>Ordering.Contracts.IntegrationEvents.OrderPlacedIntegrationEvent</c>, so a consuming
/// module's Application layer can reference the DTO shape without ever referencing
/// Ordering.Domain or Ordering.Infrastructure.
///
/// Integration events are published through the transactional Outbox (Phase 2 foundation,
/// Phase 10 processor) — never directly after <c>SaveChangesAsync</c> — so "the order was saved"
/// and "the event will eventually be published" are never allowed to disagree.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}
