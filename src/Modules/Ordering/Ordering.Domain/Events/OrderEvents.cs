using SharedKernel.Primitives;

namespace Ordering.Domain.Events;

public sealed record OrderPlacedDomainEvent(Guid OrderId) : DomainEvent;

public sealed record OrderPaidDomainEvent(Guid OrderId) : DomainEvent;

public sealed record OrderCancelledDomainEvent(Guid OrderId, string Reason) : DomainEvent;

public sealed record OrderShippedDomainEvent(Guid OrderId, string? TrackingNumber) : DomainEvent;
