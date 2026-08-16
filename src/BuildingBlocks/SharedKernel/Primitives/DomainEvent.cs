namespace SharedKernel.Primitives;

/// <summary>
/// Convenience base for domain events. Concrete events are records so they get free
/// structural equality and immutability — e.g. <c>OrderPaidDomainEvent(Guid OrderId) : DomainEvent</c>.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
