namespace Catalog.Application.Products;

/// <summary>Read-only, projection-based access for the storefront — implemented in
/// Catalog.Infrastructure with EF Core <c>.Select()</c> projections straight to DTOs.</summary>
public interface IProductQueries
{
    Task<ProductDetailsDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<ProductSearchResultDto> SearchAsync(ProductSearchCriteria criteria, CancellationToken cancellationToken = default);
}
