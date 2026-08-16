using Infrastructure;
using Inventory.Contracts;
using Messaging;
using SharedKernel.Results;

namespace Inventory.Application.Stock;

public sealed class ReserveStockCommandHandler : IRequestHandler<ReserveStockCommand, Unit>
{
    private readonly IStockItemRepository _repository;
    private readonly IInventoryUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReserveStockCommandHandler(IStockItemRepository repository, IInventoryUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(ReserveStockCommand request, CancellationToken cancellationToken = default)
    {
        var stockItem = await _repository.GetByProductVariantIdAsync(request.ProductVariantId, cancellationToken);
        if (stockItem is null)
        {
            return Result.Failure<Unit>(Error.NotFound(
                "StockItem.NotFound", $"No stock is tracked for product variant '{request.ProductVariantId}'."));
        }

        var reserveResult = stockItem.Reserve(request.Quantity, _dateTimeProvider.UtcNow, request.ReferenceId);
        if (reserveResult.IsFailure)
        {
            return Result.Failure<Unit>(reserveResult.Error);
        }

        // A DbUpdateConcurrencyException here (two concurrent checkouts racing the same variant)
        // surfaces as SharedKernel.Exceptions.ConflictException — translated by
        // Inventory.Infrastructure's unit of work, which is the only layer allowed to know about
        // EF Core (see docs/architecture.md). Store.Web's GlobalExceptionHandler already maps
        // ConflictException to HTTP 409.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(Unit.Value);
    }
}
