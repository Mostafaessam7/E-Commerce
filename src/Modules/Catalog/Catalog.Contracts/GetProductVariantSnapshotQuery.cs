using Messaging;

namespace Catalog.Contracts;

/// <summary>
/// A point-in-time read of one variant's current price/availability-for-sale — what another
/// module (Ordering, at checkout) is allowed to know about a Catalog product, dispatched through
/// the shared <c>IDispatcher</c> (ADR-014). Never the full <c>ProductVariant</c> entity: that
/// would leak Catalog.Domain across the module boundary.
/// </summary>
public sealed record ProductVariantSnapshotDto(
    Guid ProductVariantId,
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Price,
    decimal? SalePrice,
    string Currency,
    bool IsPurchasable,
    string? PrimaryImageUrl);

public sealed record GetProductVariantSnapshotQuery(Guid ProductVariantId) : IQuery<ProductVariantSnapshotDto>;
