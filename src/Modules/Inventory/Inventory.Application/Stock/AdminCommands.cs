using Infrastructure;
using Messaging;
using SharedKernel.Results;

namespace Inventory.Application.Stock;

/// <summary>Manual stock correction (Section 5's "Stock Adjustment") — wraps
/// <c>StockItem.AdjustTo</c>, same load/call/save shape as every other command handler here.</summary>
public sealed record AdjustStockCommand(Guid ProductVariantId, int NewQuantityOnHand, string Reason) : ICommand<Unit>;

public sealed class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, Unit>
{
    private readonly IStockItemRepository _repository;
    private readonly IInventoryUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AdjustStockCommandHandler(IStockItemRepository repository, IInventoryUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(AdjustStockCommand request, CancellationToken cancellationToken = default)
    {
        var stockItem = await _repository.GetByProductVariantIdAsync(request.ProductVariantId, cancellationToken);
        if (stockItem is null)
        {
            return Result.Failure<Unit>(Error.NotFound(
                "StockItem.NotFound", $"No stock is tracked for product variant '{request.ProductVariantId}'."));
        }

        var result = stockItem.AdjustTo(request.NewQuantityOnHand, request.Reason, _dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record StockSummaryDto(
    Guid Id, Guid ProductVariantId, int QuantityOnHand, int QuantityReserved, int AvailableQuantity,
    int LowStockThreshold, bool IsOutOfStock);

public sealed record StockSearchResultDto(IReadOnlyList<StockSummaryDto> Items, int TotalCount, int Page, int PageSize);

/// <summary>Read-only listing for the admin Stock page — same read/write split as
/// <c>Catalog.Application.Products.IProductQueries</c>.</summary>
public interface IStockQueries
{
    Task<StockSearchResultDto> SearchAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

public sealed record SearchStockQuery(int Page = 1, int PageSize = 20) : IQuery<StockSearchResultDto>;

public sealed class SearchStockQueryHandler : IRequestHandler<SearchStockQuery, StockSearchResultDto>
{
    private readonly IStockQueries _queries;

    public SearchStockQueryHandler(IStockQueries queries) => _queries = queries;

    public async Task<Result<StockSearchResultDto>> Handle(SearchStockQuery request, CancellationToken cancellationToken = default) =>
        Result.Success(await _queries.SearchAsync(request.Page, request.PageSize, cancellationToken));
}
