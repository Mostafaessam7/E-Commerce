using Messaging;
using Reviews.Application.Abstractions;
using Reviews.Domain;
using SharedKernel.Results;

namespace Reviews.Application.Reviews;

/// <summary>Storefront submission — no login required (mirrors guest checkout), no
/// "verified purchase" check against Ordering (see Review's doc comment for why not).</summary>
public sealed record SubmitReviewCommand(
    Guid ProductId, string ReviewerName, string? ReviewerEmail, int Rating, string? Title, string Body) : ICommand<Guid>;

public sealed class SubmitReviewCommandHandler : IRequestHandler<SubmitReviewCommand, Guid>
{
    private readonly IReviewRepository _repository;
    private readonly IReviewsUnitOfWork _unitOfWork;

    public SubmitReviewCommandHandler(IReviewRepository repository, IReviewsUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(SubmitReviewCommand request, CancellationToken cancellationToken = default)
    {
        var result = Review.Submit(request.ProductId, request.ReviewerName, request.ReviewerEmail, request.Rating, request.Title, request.Body);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        var review = result.Value;
        await _repository.AddAsync(review, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(review.Id);
    }
}
