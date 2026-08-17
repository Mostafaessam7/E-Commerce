using Microsoft.EntityFrameworkCore;
using Reviews.Application.Abstractions;
using Reviews.Domain;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Repositories;

internal sealed class ReviewRepository : IReviewRepository
{
    private readonly ReviewsDbContext _db;

    public ReviewRepository(ReviewsDbContext db) => _db = db;

    public async Task AddAsync(Review review, CancellationToken cancellationToken = default) =>
        await _db.Reviews.AddAsync(review, cancellationToken);

    public Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Reviews.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
}

internal sealed class ReviewsUnitOfWork : IReviewsUnitOfWork
{
    private readonly ReviewsDbContext _db;

    public ReviewsUnitOfWork(ReviewsDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
