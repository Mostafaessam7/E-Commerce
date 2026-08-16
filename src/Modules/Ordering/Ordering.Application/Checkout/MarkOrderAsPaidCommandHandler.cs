using Infrastructure;
using Messaging;
using Ordering.Application.Abstractions;
using Ordering.Contracts;
using SharedKernel.Results;

namespace Ordering.Application.Checkout;

public sealed class MarkOrderAsPaidCommandHandler : IRequestHandler<MarkOrderAsPaidCommand, Unit>
{
    private readonly IOrderRepository _repository;
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public MarkOrderAsPaidCommandHandler(IOrderRepository repository, IOrderingUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(MarkOrderAsPaidCommand request, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Order.NotFound", "Order was not found."));
        }

        var result = order.MarkAsPaid(_dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}
