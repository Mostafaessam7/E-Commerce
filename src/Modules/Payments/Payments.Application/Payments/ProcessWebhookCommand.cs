using System.Text.Json;
using Infrastructure;
using Messaging;
using Ordering.Contracts;
using Payments.Application.Abstractions;
using Payments.Contracts;
using SharedKernel.Results;

namespace Payments.Application.Payments;

public sealed record ProcessWebhookCommand(string RawPayload, string Signature) : ICommand<Unit>;

/// <summary>
/// Section 9's webhook handling in one place: signature verification first (reject before
/// touching anything else), then idempotent processing (a redelivered/duplicate event id is
/// acknowledged without reprocessing), then a guarded domain transition (defense-in-depth against
/// out-of-order delivery — <see cref="Payments.Domain.PaymentTransaction"/> itself refuses a
/// second resolution). Success dispatches <see cref="MarkOrderAsPaidCommand"/> into Ordering via
/// the shared <see cref="IDispatcher"/> (ADR-014, Payments calling Ordering this time).
/// </summary>
public sealed class ProcessWebhookCommandHandler : IRequestHandler<ProcessWebhookCommand, Unit>
{
    private readonly IPaymentGateway _gateway;
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly IWebhookEventRepository _webhookEventRepository;
    private readonly IPaymentsUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ProcessWebhookCommandHandler(
        IPaymentGateway gateway,
        IPaymentTransactionRepository paymentRepository,
        IWebhookEventRepository webhookEventRepository,
        IPaymentsUnitOfWork unitOfWork,
        IDispatcher dispatcher,
        IDateTimeProvider dateTimeProvider)
    {
        _gateway = gateway;
        _paymentRepository = paymentRepository;
        _webhookEventRepository = webhookEventRepository;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Unit>> Handle(ProcessWebhookCommand request, CancellationToken cancellationToken = default)
    {
        if (!_gateway.VerifyWebhookSignature(request.RawPayload, request.Signature))
        {
            return Result.Failure<Unit>(Error.Unauthorized("Webhook.InvalidSignature", "Webhook signature verification failed."));
        }

        WebhookPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<WebhookPayload>(request.RawPayload)
                ?? throw new JsonException("Empty payload.");
        }
        catch (JsonException)
        {
            return Result.Failure<Unit>(Error.Validation("Webhook.InvalidPayload", "Webhook payload could not be parsed."));
        }

        // Idempotency: a redelivered/duplicate event id is acknowledged without reprocessing —
        // Section 9's "Duplicate Webhook" / "Retry" requirement.
        if (await _webhookEventRepository.HasBeenProcessedAsync(_gateway.ProviderName, payload.EventId, cancellationToken))
        {
            return Result.Success(Unit.Value);
        }

        var payment = await _paymentRepository.GetByIdAsync(payload.PaymentTransactionId, cancellationToken);
        if (payment is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Payment.NotFound", "Payment transaction was not found."));
        }

        var now = _dateTimeProvider.UtcNow;
        var transitionResult = payload.EventType switch
        {
            "payment.succeeded" => payment.MarkSucceeded(payload.ProviderTransactionId ?? payload.EventId, now),
            "payment.failed" => payment.MarkFailed(payload.FailureReason ?? "Payment failed.", now),
            _ => Result.Failure(Error.Validation("Webhook.UnknownEventType", $"Unknown event type '{payload.EventType}'.")),
        };

        // Recorded regardless of the transition outcome — a rejected duplicate/out-of-order
        // event still counts as "processed" for idempotency purposes, so it's never retried.
        await _webhookEventRepository.MarkProcessedAsync(_gateway.ProviderName, payload.EventId, now, cancellationToken);

        if (transitionResult.IsSuccess && payload.EventType == "payment.succeeded")
        {
            await _dispatcher.Send(new MarkOrderAsPaidCommand(payment.OrderId), cancellationToken);
            _unitOfWork.EnqueueIntegrationEvent(new PaymentSucceededIntegrationEvent(payment.Id, payment.OrderId, payment.Amount.Amount, payment.Amount.Currency));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return transitionResult.IsSuccess
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(transitionResult.Error);
    }
}
