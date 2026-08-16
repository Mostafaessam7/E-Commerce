using Infrastructure;
using Messaging;
using Payments.Application.Abstractions;
using SharedKernel.Results;

namespace Payments.Application.Payments;

public sealed record RefundPaymentCommand(Guid PaymentTransactionId, decimal Amount, string? Reason) : ICommand<Unit>;

public sealed class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, Unit>
{
    private readonly IPaymentGateway _gateway;
    private readonly IPaymentTransactionRepository _repository;
    private readonly IPaymentsUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RefundPaymentCommandHandler(IPaymentGateway gateway, IPaymentTransactionRepository repository, IPaymentsUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _gateway = gateway;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(RefundPaymentCommand request, CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByIdAsync(request.PaymentTransactionId, cancellationToken);
        if (payment is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Payment.NotFound", "Payment transaction was not found."));
        }

        if (payment.ProviderTransactionId is null)
        {
            return Result.Failure<Unit>(Error.Conflict("Payment.NoProviderTransaction", "This payment has no provider transaction to refund."));
        }

        var gatewayResult = await _gateway.RefundAsync(payment.ProviderTransactionId, request.Amount, payment.Amount.Currency, cancellationToken);
        if (gatewayResult.IsFailure)
        {
            return Result.Failure<Unit>(gatewayResult.Error);
        }

        var refundResult = payment.Refund(request.Amount, request.Reason, _dateTimeProvider.UtcNow);
        if (refundResult.IsFailure)
        {
            return Result.Failure<Unit>(refundResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}
