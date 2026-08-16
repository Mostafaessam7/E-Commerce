using SharedKernel.Primitives;

namespace Catalog.Domain.Events;

public sealed record ProductCreatedDomainEvent(Guid ProductId) : DomainEvent;

public sealed record ProductPublishedDomainEvent(Guid ProductId) : DomainEvent;
