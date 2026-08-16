using Messaging;
using SharedKernel.Results;

namespace Catalog.Application.Products;

public sealed record GetProductBySlugQuery(string Slug) : IQuery<ProductDetailsDto>;

public sealed class GetProductBySlugQueryHandler : IRequestHandler<GetProductBySlugQuery, ProductDetailsDto>
{
    private readonly IProductQueries _queries;

    public GetProductBySlugQueryHandler(IProductQueries queries) => _queries = queries;

    public async Task<Result<ProductDetailsDto>> Handle(GetProductBySlugQuery request, CancellationToken cancellationToken = default)
    {
        var product = await _queries.GetBySlugAsync(request.Slug, cancellationToken);

        return product is null
            ? Result.Failure<ProductDetailsDto>(Error.NotFound("Product.NotFound", $"Product '{request.Slug}' was not found."))
            : Result.Success(product);
    }
}
