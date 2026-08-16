using Messaging;
using Payments.Application.Abstractions;
using Payments.Domain;
using SharedKernel.Results;

namespace Payments.Application.Payments;

public sealed record InitializePaymentResultDto(Guid PaymentTransactionId, string ProviderIntentId, string? RedirectUrl);

public sealed record InitializePaymentCommand(Guid OrderId, decimal Amount, string Currency) : ICommand<InitializePaymentResultDto>;

public sealed class InitializePaymentCommandHandler : IRequestHandler<InitializePaymentCommand, InitializePaymentResultDto>
{
    private readonly IPaymentGateway _gateway;
    private readonly IPaymentTransactionRepository _repository;
    private readonly IPaymentsUnitOfWork _unitOfWork;

    public InitializePaymentCommandHandler(IPaymentGateway gateway, IPaymentTransactionRepository repository, IPaymentsUnitOfWork unitOfWork)
    {
        _gateway = gateway;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<InitializePaymentResultDto>> Handle(InitializePaymentCommand request, CancellationToken cancellationToken = default)
    {
        var intentResult = await _gateway.CreateIntentAsync(request.OrderId, request.Amount, request.Currency, cancellationToken);
        if (intentResult.IsFailure)
        {
            return Result.Failure<InitializePaymentResultDto>(intentResult.Error);
        }

        var paymentResult = PaymentTransaction.Initialize(
            request.OrderId, request.Amount, request.Currency, _gateway.ProviderName, intentResult.Value.ProviderIntentId);

        if (paymentResult.IsFailure)
        {
            return Result.Failure<InitializePaymentResultDto>(paymentResult.Error);
        }

        var payment = paymentResult.Value;
        await _repository.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new InitializePaymentResultDto(payment.Id, payment.ProviderIntentId, intentResult.Value.RedirectUrl));
    }
}
