using Catalog.Application.Brands;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Repositories;

internal sealed class BrandRepository : IBrandRepository
{
    private readonly CatalogDbContext _db;

    public BrandRepository(CatalogDbContext db) => _db = db;

    public async Task AddAsync(Brand brand, CancellationToken cancellationToken = default) =>
        await _db.Brands.AddAsync(brand, cancellationToken);

    public Task<Brand?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
}

internal sealed class BrandQueries : IBrandQueries
{
    private readonly CatalogDbContext _db;

    public BrandQueries(CatalogDbContext db) => _db = db;

    public async Task<IReadOnlyList<BrandDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = _db.Brands.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(b => b.IsActive);
        }

        return await query
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto(b.Id, b.Name, b.Slug.Value, b.IsActive))
            .ToListAsync(cancellationToken);
    }
}
