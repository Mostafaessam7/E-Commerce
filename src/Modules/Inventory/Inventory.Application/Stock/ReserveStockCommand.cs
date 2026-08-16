using Infrastructure;
using Inventory.Domain;
using Messaging;
using SharedKernel.Results;

namespace Inventory.Application.Stock;

/// <summary>
/// Reserves stock for one order line at checkout. <see cref="ReferenceId"/> is typically the
/// OrderId — kept as a plain Guid, not a reference to Ordering's aggregate (see module boundary
/// rules).
/// </summary>
public sealed record ReserveStockCommand(Guid ProductVariantId, int Quantity, Guid? ReferenceId) : ICommand<Unit>;

/// <summary>Marker for "no meaningful return value" — <see cref="Messaging.IRequestHandler{TRequest,TResponse}"/>
/// always needs a TResponse, and a command whose only interesting outcome is success/failure
/// still needs one to flow through the same Result&lt;T&gt; pipeline as every other request.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}

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
