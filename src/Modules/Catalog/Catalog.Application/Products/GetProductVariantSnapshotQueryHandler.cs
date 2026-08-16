using Catalog.Contracts;
using Messaging;
using SharedKernel.Results;

namespace Catalog.Application.Products;

public sealed class GetProductVariantSnapshotQueryHandler : IRequestHandler<GetProductVariantSnapshotQuery, ProductVariantSnapshotDto>
{
    private readonly IProductQueries _queries;

    public GetProductVariantSnapshotQueryHandler(IProductQueries queries) => _queries = queries;

    public async Task<Result<ProductVariantSnapshotDto>> Handle(GetProductVariantSnapshotQuery request, CancellationToken cancellationToken = default)
    {
        var snapshot = await _queries.GetVariantSnapshotAsync(request.ProductVariantId, cancellationToken);

        return snapshot is null
            ? Result.Failure<ProductVariantSnapshotDto>(Error.NotFound(
                "ProductVariant.NotFound", $"Product variant '{request.ProductVariantId}' was not found."))
            : Result.Success(snapshot);
    }
}
