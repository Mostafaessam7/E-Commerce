using Infrastructure;
using Messaging;
using Ordering.Application.Abstractions;
using SharedKernel.Results;

namespace Ordering.Application.Checkout;

/// <summary>
/// Admin-only order status transitions — thin wrappers over <see cref="Ordering.Domain.Order"/>'s
/// named transition methods (Section 8), same shape as <see cref="MarkOrderAsPaidCommandHandler"/>:
/// load, call the one domain method, save. No new business rules here — the aggregate already
/// owns them (illegal transitions come back as a Result.Failure, not an exception).
/// </summary>
public sealed record ConfirmOrderCommand(Guid OrderId) : ICommand<Unit>;

public sealed class ConfirmOrderCommandHandler : IRequestHandler<ConfirmOrderCommand, Unit>
{
    private readonly IOrderRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ConfirmOrderCommandHandler(IOrderRepository repository, IOrderingUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(ConfirmOrderCommand request, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Order.NotFound", "Order was not found."));
        }

        var result = order.Confirm(_dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record StartProcessingOrderCommand(Guid OrderId) : ICommand<Unit>;

public sealed class StartProcessingOrderCommandHandler : IRequestHandler<StartProcessingOrderCommand, Unit>
{
    private readonly IOrderRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public StartProcessingOrderCommandHandler(IOrderRepository repository, IOrderingUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(StartProcessingOrderCommand request, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Order.NotFound", "Order was not found."));
        }

        var result = order.StartProcessing(_dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record ShipOrderCommand(Guid OrderId, string? TrackingNumber) : ICommand<Unit>;

public sealed class ShipOrderCommandHandler : IRequestHandler<ShipOrderCommand, Unit>
{
    private readonly IOrderRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ShipOrderCommandHandler(IOrderRepository repository, IOrderingUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(ShipOrderCommand request, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Order.NotFound", "Order was not found."));
        }

        var result = order.MarkAsShipped(_dateTimeProvider.UtcNow, request.TrackingNumber);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record DeliverOrderCommand(Guid OrderId) : ICommand<Unit>;

public sealed class DeliverOrderCommandHandler : IRequestHandler<DeliverOrderCommand, Unit>
{
    private readonly IOrderRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeliverOrderCommandHandler(IOrderRepository repository, IOrderingUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(DeliverOrderCommand request, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Order.NotFound", "Order was not found."));
        }

        var result = order.MarkAsDelivered(_dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record CancelOrderCommand(Guid OrderId, string Reason) : ICommand<Unit>;

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Unit>
{
    private readonly IOrderRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CancelOrderCommandHandler(IOrderRepository repository, IOrderingUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(CancelOrderCommand request, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Order.NotFound", "Order was not found."));
        }

        var result = order.Cancel(request.Reason, _dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record OrderSummaryDto(
    Guid Id, string OrderNumber, string Status, string PaymentStatus, decimal Total, string Currency, DateTime PlacedAtUtc);

public sealed record OrderSearchCriteria(string? Status = null, int Page = 1, int PageSize = 20);

public sealed record OrderSearchResultDto(IReadOnlyList<OrderSummaryDto> Items, int TotalCount, int Page, int PageSize);

/// <summary>Read-only, projection-based listing for the admin Orders page — mirrors
/// <c>Catalog.Application.Products.IProductQueries</c>'s split between write-side repository and
/// read-side projections.</summary>
public interface IOrderQueries
{
    Task<OrderSearchResultDto> SearchAsync(OrderSearchCriteria criteria, CancellationToken cancellationToken = default);
}

public sealed record SearchOrdersQuery(OrderSearchCriteria Criteria) : IQuery<OrderSearchResultDto>;

public sealed class SearchOrdersQueryHandler : IRequestHandler<SearchOrdersQuery, OrderSearchResultDto>
{
    private readonly IOrderQueries _queries;

    public SearchOrdersQueryHandler(IOrderQueries queries) => _queries = queries;

    public async Task<Result<OrderSearchResultDto>> Handle(SearchOrdersQuery request, CancellationToken cancellationToken = default) =>
        Result.Success(await _queries.SearchAsync(request.Criteria, cancellationToken));
}
