using Microsoft.EntityFrameworkCore;
using Reviews.Application.Reviews;
using Reviews.Domain;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Repositories;

internal sealed class ReviewsQueries : IReviewsQueries
{
    private readonly ReviewsDbContext _db;

    public ReviewsQueries(ReviewsDbContext db) => _db = db;

    public async Task<IReadOnlyList<ReviewDto>> ListAsync(bool pendingOnly, CancellationToken cancellationToken = default)
    {
        var query = _db.Reviews.AsNoTracking().AsQueryable();
        if (pendingOnly)
        {
            query = query.Where(r => r.Status == ReviewStatus.Pending);
        }

        return await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ReviewDto(r.Id, r.ProductId, r.ReviewerName, r.Rating, r.Title, r.Body, r.Status.ToString(), r.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductReviewsDto> GetForProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var approved = await _db.Reviews.AsNoTracking()
            .Where(r => r.ProductId == productId && r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ReviewDto(r.Id, r.ProductId, r.ReviewerName, r.Rating, r.Title, r.Body, r.Status.ToString(), r.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        double? average = approved.Count > 0 ? approved.Average(r => r.Rating) : null;

        return new ProductReviewsDto(productId, approved, approved.Count, average);
    }
}
