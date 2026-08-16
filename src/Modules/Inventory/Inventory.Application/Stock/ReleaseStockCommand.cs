using Infrastructure;
using Inventory.Contracts;
using Messaging;
using SharedKernel.Results;

namespace Inventory.Application.Stock;

public sealed class ReleaseStockCommandHandler : IRequestHandler<ReleaseStockCommand, Unit>
{
    private readonly IStockItemRepository _repository;
    private readonly IInventoryUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReleaseStockCommandHandler(IStockItemRepository repository, IInventoryUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(ReleaseStockCommand request, CancellationToken cancellationToken = default)
    {
        var stockItem = await _repository.GetByProductVariantIdAsync(request.ProductVariantId, cancellationToken);
        if (stockItem is null)
        {
            return Result.Failure<Unit>(Error.NotFound(
                "StockItem.NotFound", $"No stock is tracked for product variant '{request.ProductVariantId}'."));
        }

        var releaseResult = stockItem.Release(request.Quantity, _dateTimeProvider.UtcNow, request.ReferenceId);
        if (releaseResult.IsFailure)
        {
            return Result.Failure<Unit>(releaseResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}
