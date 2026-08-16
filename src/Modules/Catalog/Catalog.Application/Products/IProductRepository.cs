using Catalog.Domain;

namespace Catalog.Application.Products;

/// <summary>
/// Write-side access to the Product aggregate — command handlers only. Not a generic
/// repository: only the operations command handlers actually need. Read-side (listing/search
/// pages) goes through <see cref="IProductQueries"/> instead, which projects straight to DTOs
/// (Section 26: avoid loading full aggregates for reads).
/// </summary>
public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Per-module unit of work — see docs/architecture.md on why this isn't a single shared
/// <c>IUnitOfWork</c> across modules (DI ambiguity with ten different DbContexts).</summary>
public interface ICatalogUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
