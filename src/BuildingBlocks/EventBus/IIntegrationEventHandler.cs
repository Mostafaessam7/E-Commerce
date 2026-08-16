namespace EventBus;

/// <summary>
/// Handles one integration event inside the consuming module. Implementations must be
/// idempotent — the Outbox processor guarantees at-least-once delivery (a crash between
/// "handler ran" and "marked processed" replays the event), never exactly-once. A typical
/// implementation checks a "have I already applied EventId X" record before acting, e.g.
/// Inventory's handler for <c>OrderPlacedIntegrationEvent</c> keyed on <c>EventId</c> before
/// reserving stock again.
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default);
}
