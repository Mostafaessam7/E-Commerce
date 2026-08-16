using Catalog.Contracts;

namespace Catalog.Application.Products;

/// <summary>Read-only, projection-based access for the storefront — implemented in
/// Catalog.Infrastructure with EF Core <c>.Select()</c> projections straight to DTOs.</summary>
public interface IProductQueries
{
    Task<ProductDetailsDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<ProductSearchResultDto> SearchAsync(ProductSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Backs <see cref="GetProductVariantSnapshotQuery"/> — the cross-module read other
    /// modules dispatch at checkout (ADR-014).</summary>
    Task<ProductVariantSnapshotDto?> GetVariantSnapshotAsync(Guid productVariantId, CancellationToken cancellationToken = default);
}
