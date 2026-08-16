namespace EventBus;

/// <summary>
/// Publishes integration events to whatever transport backs the Outbox processor. Phase 1 ships
/// only this abstraction; application/domain code depends on <see cref="IEventBus"/>, never on a
/// concrete transport. The Phase 10 implementation is an in-process dispatcher (this is a
/// modular monolith on one deployable, not a distributed system — see the "no microservices
/// without a real scaling need" rule), reached only through the Outbox table so publishing is
/// transactional with the write that caused it. Swapping to a real broker (e.g. Azure Service
/// Bus, RabbitMQ) later means adding a new <see cref="IEventBus"/> implementation, not touching
/// any module's Domain or Application code.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
