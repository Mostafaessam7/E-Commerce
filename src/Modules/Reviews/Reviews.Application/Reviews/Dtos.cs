namespace Reviews.Application.Reviews;

public sealed record ReviewDto(
    Guid Id, Guid ProductId, string ReviewerName, int Rating, string? Title, string Body,
    string Status, DateTime CreatedAtUtc);

/// <summary>Storefront projection for one product's page: approved reviews plus the aggregate
/// rating summary they produce — a single round trip instead of two.</summary>
public sealed record ProductReviewsDto(
    Guid ProductId, IReadOnlyList<ReviewDto> Reviews, int ApprovedCount, double? AverageRating);
