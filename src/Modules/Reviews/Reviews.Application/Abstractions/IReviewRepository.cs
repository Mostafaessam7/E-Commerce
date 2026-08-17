using Reviews.Domain;

namespace Reviews.Application.Abstractions;

public interface IReviewRepository
{
    Task AddAsync(Review review, CancellationToken cancellationToken = default);

    Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IReviewsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
