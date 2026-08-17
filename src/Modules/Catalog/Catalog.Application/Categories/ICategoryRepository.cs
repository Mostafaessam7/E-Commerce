using Catalog.Domain;

namespace Catalog.Application.Categories;

public interface ICategoryRepository
{
    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ICategoryQueries
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);
}
