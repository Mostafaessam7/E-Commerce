using SharedKernel.Primitives;

namespace Inventory.Domain.Events;

public sealed record StockReservedDomainEvent(Guid StockItemId, Guid ProductVariantId, int Quantity) : DomainEvent;

public sealed record StockReservationFailedDomainEvent(Guid StockItemId, Guid ProductVariantId, int RequestedQuantity) : DomainEvent;

/// <summary>Raised once available quantity drops to/below the threshold — Section 21's "Stock
/// Alerts" background job (not built yet) is the intended consumer via the Outbox.</summary>
public sealed record StockLowDomainEvent(Guid StockItemId, Guid ProductVariantId, int AvailableQuantity, int Threshold) : DomainEvent;
