using Messaging;
using Payments.Application.Abstractions;
using SharedKernel.Results;

namespace Payments.Application.Payments;

public sealed record PaymentDto(Guid Id, Guid OrderId, string Provider, string Status, decimal Amount, decimal RefundedAmount, string Currency, string? FailureReason);

public sealed record GetPaymentQuery(Guid PaymentTransactionId) : IQuery<PaymentDto>;

public sealed class GetPaymentQueryHandler : IRequestHandler<GetPaymentQuery, PaymentDto>
{
    private readonly IPaymentTransactionRepository _repository;

    public GetPaymentQueryHandler(IPaymentTransactionRepository repository) => _repository = repository;

    public async Task<Result<PaymentDto>> Handle(GetPaymentQuery request, CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByIdAsync(request.PaymentTransactionId, cancellationToken);

        return payment is null
            ? Result.Failure<PaymentDto>(Error.NotFound("Payment.NotFound", "Payment transaction was not found."))
            : Result.Success(new PaymentDto(
                payment.Id, payment.OrderId, payment.Provider, payment.Status.ToString(),
                payment.Amount.Amount, payment.RefundedAmount.Amount, payment.Amount.Currency, payment.FailureReason));
    }
}
