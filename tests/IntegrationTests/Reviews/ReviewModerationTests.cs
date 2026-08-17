using FluentAssertions;
using Infrastructure;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Reviews;
using Reviews.Infrastructure;
using Reviews.Infrastructure.Persistence;
using Security;

namespace IntegrationTests.Reviews;

/// <summary>
/// End-to-end against the real local DB: a submitted review starts Pending and is invisible to
/// the storefront query until an admin approves it — proves the moderation gate is real, not just
/// a status column nobody reads.
/// </summary>
public sealed class ReviewModerationTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";

    private ServiceProvider _provider = null!;
    private Guid _reviewId;
    private Guid _productId;

    public Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("ConnectionStrings:Database", ConnectionString)])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSecurityCore();
        services.AddMessagingCore();
        services.AddReviewsModule(configuration);

        _provider = services.BuildServiceProvider();
        _productId = Guid.NewGuid();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReviewsDbContext>();
        await db.Reviews.Where(r => r.Id == _reviewId).ExecuteDeleteAsync();

        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Submitted_review_is_pending_and_invisible_to_the_storefront_until_approved()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var submitResult = await dispatcher.Send(
            new SubmitReviewCommand(_productId, "Integration Tester", "tester@example.com", 4, "Pretty good", "Solid product overall."));
        submitResult.IsSuccess.Should().BeTrue();
        _reviewId = submitResult.Value;

        var beforeApproval = await dispatcher.Send(new GetProductReviewsQuery(_productId));
        beforeApproval.Value.Reviews.Should().BeEmpty("a Pending review must not appear on the storefront");
        beforeApproval.Value.AverageRating.Should().BeNull();

        var pending = await dispatcher.Send(new ListReviewsQuery(PendingOnly: true));
        pending.Value.Should().Contain(r => r.Id == _reviewId);

        var approveResult = await dispatcher.Send(new ApproveReviewCommand(_reviewId));
        approveResult.IsSuccess.Should().BeTrue();

        var afterApproval = await dispatcher.Send(new GetProductReviewsQuery(_productId));
        afterApproval.Value.Reviews.Should().ContainSingle(r => r.Id == _reviewId);
        afterApproval.Value.AverageRating.Should().Be(4);
    }

    [Fact]
    public async Task Rejected_review_never_appears_on_the_storefront_and_cannot_be_moderated_again()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var submitResult = await dispatcher.Send(
            new SubmitReviewCommand(_productId, "Another Tester", null, 1, null, "Not what I expected."));
        _reviewId = submitResult.Value;

        var rejectResult = await dispatcher.Send(new RejectReviewCommand(_reviewId));
        rejectResult.IsSuccess.Should().BeTrue();

        var afterRejection = await dispatcher.Send(new GetProductReviewsQuery(_productId));
        afterRejection.Value.Reviews.Should().BeEmpty();

        var secondApprove = await dispatcher.Send(new ApproveReviewCommand(_reviewId));
        secondApprove.IsFailure.Should().BeTrue("a rejected review is a terminal state, not re-moderatable");
        secondApprove.Error.Code.Should().Be("Review.NotPending");
    }
}
