namespace SharedKernel.Primitives;

/// <summary>
/// Marks a notification that something meaningful happened inside a single aggregate.
/// Domain events are raised and dispatched in-process (same transaction, same request) —
/// they are NOT the mechanism for cross-module communication. That's what
/// <c>EventBus.IIntegrationEvent</c> + the Outbox are for. A domain event handler that needs
/// to tell other modules about the change publishes an integration event instead.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}
