using Catalog.Contracts;
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
    int LowStockThreshold, bool IsOutOfStock, string? ProductName = null, string? Sku = null);

public sealed record StockSearchResultDto(IReadOnlyList<StockSummaryDto> Items, int TotalCount, int Page, int PageSize);

/// <summary>Read-only listing for the admin Stock page — same read/write split as
/// <c>Catalog.Application.Products.IProductQueries</c>.</summary>
public interface IStockQueries
{
    Task<StockSearchResultDto> SearchAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

public sealed record SearchStockQuery(int Page = 1, int PageSize = 20) : IQuery<StockSearchResultDto>;

/// <summary>
/// Enriches each row with the product's name/SKU via <see cref="GetProductVariantSnapshotQuery"/>
/// (ADR-014) — <see cref="IStockQueries"/> itself only ever knows Inventory's own data (a
/// <c>ProductVariantId</c> Guid, deliberately no FK/navigation into Catalog). Before this
/// (Phase 32, ADR-043) the admin Stock page had nothing to show but the raw Guid, which nobody can
/// actually recognize a product by.
/// </summary>
public sealed class SearchStockQueryHandler : IRequestHandler<SearchStockQuery, StockSearchResultDto>
{
    private readonly IStockQueries _queries;
    private readonly IDispatcher _dispatcher;

    public SearchStockQueryHandler(IStockQueries queries, IDispatcher dispatcher)
    {
        _queries = queries;
        _dispatcher = dispatcher;
    }

    public async Task<Result<StockSearchResultDto>> Handle(SearchStockQuery request, CancellationToken cancellationToken = default)
    {
        var page = await _queries.SearchAsync(request.Page, request.PageSize, cancellationToken);

        var enriched = new List<StockSummaryDto>(page.Items.Count);
        foreach (var item in page.Items)
        {
            var snapshot = await _dispatcher.Send(new GetProductVariantSnapshotQuery(item.ProductVariantId), cancellationToken);
            enriched.Add(snapshot.IsSuccess
                ? item with { ProductName = snapshot.Value.ProductName, Sku = snapshot.Value.Sku }
                : item);
        }

        return Result.Success(page with { Items = enriched });
    }
}
