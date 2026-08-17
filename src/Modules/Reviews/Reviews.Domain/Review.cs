using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;

namespace Reviews.Domain;

public enum ReviewStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// Aggregate root for a product review (Section: "product reviews/ratings"). No "verified
/// purchase" concept — a review isn't checked against Ordering for a real completed order before
/// being accepted (a real follow-up, not attempted here, see docs/decisions.md). Every review
/// starts <see cref="ReviewStatus.Pending"/> and only appears on the storefront once an admin
/// <see cref="Approve"/>s it — the same "don't trust unmoderated input" instinct as everywhere
/// else, applied to free-text content instead of a price or a stock count.
/// </summary>
public sealed class Review : AggregateRoot<Guid>
{
    private Review(Guid id, Guid productId, string reviewerName, string? reviewerEmail, int rating, string? title, string body)
        : base(id)
    {
        ProductId = productId;
        ReviewerName = reviewerName;
        ReviewerEmail = reviewerEmail;
        Rating = rating;
        Title = title;
        Body = body;
        Status = ReviewStatus.Pending;
    }

    private Review()
    {
    }

    public Guid ProductId { get; private set; }

    public string ReviewerName { get; private set; } = null!;

    public string? ReviewerEmail { get; private set; }

    public int Rating { get; private set; }

    public string? Title { get; private set; }

    public string Body { get; private set; } = null!;

    public ReviewStatus Status { get; private set; }

    public static Result<Review> Submit(
        Guid productId, string reviewerName, string? reviewerEmail, int rating, string? title, string body)
    {
        Guard.Against.NullOrWhiteSpace(reviewerName, nameof(reviewerName));
        Guard.Against.NullOrWhiteSpace(body, nameof(body));

        if (rating is < 1 or > 5)
        {
            return Result.Failure<Review>(Error.Validation("Review.InvalidRating", "Rating must be between 1 and 5."));
        }

        return Result.Success(new Review(Guid.NewGuid(), productId, reviewerName, reviewerEmail, rating, title, body));
    }

    public Result Approve()
    {
        if (Status != ReviewStatus.Pending)
        {
            return Result.Failure(Error.Validation("Review.NotPending", "Only a pending review can be approved."));
        }

        Status = ReviewStatus.Approved;
        return Result.Success();
    }

    public Result Reject()
    {
        if (Status != ReviewStatus.Pending)
        {
            return Result.Failure(Error.Validation("Review.NotPending", "Only a pending review can be rejected."));
        }

        Status = ReviewStatus.Rejected;
        return Result.Success();
    }
}
