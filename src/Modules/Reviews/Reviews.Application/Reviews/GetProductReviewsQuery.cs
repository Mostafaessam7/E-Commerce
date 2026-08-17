using Messaging;
using SharedKernel.Results;

namespace Reviews.Application.Reviews;

public sealed record GetProductReviewsQuery(Guid ProductId) : IQuery<ProductReviewsDto>;

public sealed class GetProductReviewsQueryHandler : IRequestHandler<GetProductReviewsQuery, ProductReviewsDto>
{
    private readonly IReviewsQueries _queries;

    public GetProductReviewsQueryHandler(IReviewsQueries queries) => _queries = queries;

    public async Task<Result<ProductReviewsDto>> Handle(GetProductReviewsQuery request, CancellationToken cancellationToken = default) =>
        Result.Success(await _queries.GetForProductAsync(request.ProductId, cancellationToken));
}
