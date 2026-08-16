using Messaging;
using SharedKernel.Results;

namespace Inventory.Application.Stock;

public sealed record StockLevelDto(Guid ProductVariantId, int QuantityOnHand, int QuantityReserved, int AvailableQuantity, bool IsOutOfStock);

public sealed record GetStockQuery(Guid ProductVariantId) : IQuery<StockLevelDto>;

public sealed class GetStockQueryHandler : IRequestHandler<GetStockQuery, StockLevelDto>
{
    private readonly IStockItemRepository _repository;

    public GetStockQueryHandler(IStockItemRepository repository) => _repository = repository;

    public async Task<Result<StockLevelDto>> Handle(GetStockQuery request, CancellationToken cancellationToken = default)
    {
        var stockItem = await _repository.GetByProductVariantIdAsync(request.ProductVariantId, cancellationToken);

        return stockItem is null
            ? Result.Failure<StockLevelDto>(Error.NotFound(
                "StockItem.NotFound", $"No stock is tracked for product variant '{request.ProductVariantId}'."))
            : Result.Success(new StockLevelDto(
                stockItem.ProductVariantId, stockItem.QuantityOnHand, stockItem.QuantityReserved,
                stockItem.AvailableQuantity, stockItem.IsOutOfStock));
    }
}
