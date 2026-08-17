using FluentAssertions;
using Reviews.Domain;

namespace UnitTests.Reviews;

public class ReviewTests
{
    [Fact]
    public void Submit_creates_a_pending_review()
    {
        var result = Review.Submit(Guid.NewGuid(), "Ahmed", "ahmed@example.com", 5, "Great!", "Loved it.");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ReviewStatus.Pending);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Submit_fails_for_a_rating_outside_1_to_5(int rating)
    {
        var result = Review.Submit(Guid.NewGuid(), "Ahmed", null, rating, null, "Body text");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Review.InvalidRating");
    }

    [Fact]
    public void Submit_throws_for_a_blank_body()
    {
        var act = () => Review.Submit(Guid.NewGuid(), "Ahmed", null, 5, null, "   ");

        act.Should().Throw<ArgumentException>("Guard.Against.NullOrWhiteSpace enforces this as an unreachable invariant, not an expected failure");
    }

    [Fact]
    public void Approve_moves_a_pending_review_to_approved()
    {
        var review = Review.Submit(Guid.NewGuid(), "Ahmed", null, 5, null, "Body text").Value;

        var result = review.Approve();

        result.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Approved);
    }

    [Fact]
    public void Reject_moves_a_pending_review_to_rejected()
    {
        var review = Review.Submit(Guid.NewGuid(), "Ahmed", null, 5, null, "Body text").Value;

        var result = review.Reject();

        result.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Rejected);
    }

    [Fact]
    public void Approve_fails_for_a_review_that_is_not_pending()
    {
        var review = Review.Submit(Guid.NewGuid(), "Ahmed", null, 5, null, "Body text").Value;
        review.Approve();

        var result = review.Approve();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Review.NotPending");
    }

    [Fact]
    public void Reject_fails_for_a_review_that_is_not_pending()
    {
        var review = Review.Submit(Guid.NewGuid(), "Ahmed", null, 5, null, "Body text").Value;
        review.Reject();

        var result = review.Reject();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Review.NotPending");
    }
}
