using SharedKernel.Primitives;

namespace Ordering.Domain.Events;

public sealed record CartItemAddedDomainEvent(Guid CartId, Guid ProductVariantId, int Quantity) : DomainEvent;
