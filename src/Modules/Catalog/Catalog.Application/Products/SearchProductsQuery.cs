using Messaging;
using SharedKernel.Results;

namespace Catalog.Application.Products;

public sealed record SearchProductsQuery(ProductSearchCriteria Criteria) : IQuery<ProductSearchResultDto>;

public sealed class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, ProductSearchResultDto>
{
    private readonly IProductQueries _queries;

    public SearchProductsQueryHandler(IProductQueries queries) => _queries = queries;

    public async Task<Result<ProductSearchResultDto>> Handle(SearchProductsQuery request, CancellationToken cancellationToken = default) =>
        Result.Success(await _queries.SearchAsync(request.Criteria, cancellationToken));
}
