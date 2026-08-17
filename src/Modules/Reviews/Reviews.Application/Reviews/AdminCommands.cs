using Messaging;
using Reviews.Application.Abstractions;
using SharedKernel.Results;

namespace Reviews.Application.Reviews;

public sealed record ApproveReviewCommand(Guid ReviewId) : ICommand<Unit>;

public sealed class ApproveReviewCommandHandler : IRequestHandler<ApproveReviewCommand, Unit>
{
    private readonly IReviewRepository _repository;
    private readonly IReviewsUnitOfWork _unitOfWork;

    public ApproveReviewCommandHandler(IReviewRepository repository, IReviewsUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(ApproveReviewCommand request, CancellationToken cancellationToken = default)
    {
        var review = await _repository.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Review.NotFound", "Review was not found."));
        }

        var result = review.Approve();
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record RejectReviewCommand(Guid ReviewId) : ICommand<Unit>;

public sealed class RejectReviewCommandHandler : IRequestHandler<RejectReviewCommand, Unit>
{
    private readonly IReviewRepository _repository;
    private readonly IReviewsUnitOfWork _unitOfWork;

    public RejectReviewCommandHandler(IReviewRepository repository, IReviewsUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(RejectReviewCommand request, CancellationToken cancellationToken = default)
    {
        var review = await _repository.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Review.NotFound", "Review was not found."));
        }

        var result = review.Reject();
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

/// <summary>Admin listing — every status, newest first (unlike the storefront's
/// <see cref="GetProductReviewsQuery"/>, which only ever returns approved ones).</summary>
public sealed record ListReviewsQuery(bool PendingOnly = false) : IQuery<IReadOnlyList<ReviewDto>>;

public sealed class ListReviewsQueryHandler : IRequestHandler<ListReviewsQuery, IReadOnlyList<ReviewDto>>
{
    private readonly IReviewsQueries _queries;

    public ListReviewsQueryHandler(IReviewsQueries queries) => _queries = queries;

    public async Task<Result<IReadOnlyList<ReviewDto>>> Handle(ListReviewsQuery request, CancellationToken cancellationToken = default) =>
        Result.Success(await _queries.ListAsync(request.PendingOnly, cancellationToken));
}

public interface IReviewsQueries
{
    Task<IReadOnlyList<ReviewDto>> ListAsync(bool pendingOnly, CancellationToken cancellationToken = default);

    Task<ProductReviewsDto> GetForProductAsync(Guid productId, CancellationToken cancellationToken = default);
}
