using Catalog.Domain;

namespace Catalog.Application.Brands;

public interface IBrandRepository
{
    Task AddAsync(Brand brand, CancellationToken cancellationToken = default);

    Task<Brand?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IBrandQueries
{
    Task<IReadOnlyList<BrandDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);
}
