using Catalog.Application.Categories;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Repositories;

internal sealed class CategoryRepository : ICategoryRepository
{
    private readonly CatalogDbContext _db;

    public CategoryRepository(CatalogDbContext db) => _db = db;

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default) =>
        await _db.Categories.AddAsync(category, cancellationToken);

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
}

internal sealed class CategoryQueries : ICategoryQueries
{
    private readonly CatalogDbContext _db;

    public CategoryQueries(CatalogDbContext db) => _db = db;

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug.Value, c.ParentId, c.IsActive))
            .ToListAsync(cancellationToken);
    }
}
