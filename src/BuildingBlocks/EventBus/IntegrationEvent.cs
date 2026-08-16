namespace EventBus;

/// <summary>
/// Convenience base for concrete integration events. Prefer a record:
/// <c>public sealed record OrderPlacedIntegrationEvent(Guid OrderId, string OrderNumber) : IntegrationEvent;</c>
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
