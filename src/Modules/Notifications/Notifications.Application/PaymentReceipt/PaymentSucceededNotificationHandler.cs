using EventBus;
using Infrastructure;
using Messaging;
using Notifications.Application.Abstractions;
using Notifications.Domain;
using Ordering.Contracts;
using Payments.Contracts;

namespace Notifications.Application.PaymentReceipt;

/// <summary>
/// Reacts to <see cref="PaymentSucceededIntegrationEvent"/> — payment receipt email.
/// <see cref="PaymentSucceededIntegrationEvent"/> carries no email (Payments never collects one —
/// docs/events.md), so this dispatches <see cref="GetOrderContactInfoQuery"/> into Ordering via
/// the shared <c>IDispatcher</c> (ADR-014) to get it. If that lookup itself fails (order
/// deleted/inconsistent — shouldn't happen, but the Outbox's at-least-once delivery means this
/// could theoretically run against stale state), the notification is logged as Failed rather than
/// throwing — a missing receipt email is not worth crashing the Outbox processor's batch over.
/// </summary>
public sealed class PaymentSucceededNotificationHandler : IIntegrationEventHandler<PaymentSucceededIntegrationEvent>
{
    private readonly IDispatcher _dispatcher;
    private readonly INotificationSender _sender;
    private readonly INotificationLogRepository _repository;
    private readonly INotificationsUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PaymentSucceededNotificationHandler(
        IDispatcher dispatcher,
        INotificationSender sender,
        INotificationLogRepository repository,
        INotificationsUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _dispatcher = dispatcher;
        _sender = sender;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task HandleAsync(PaymentSucceededIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var contactResult = await _dispatcher.Send(new GetOrderContactInfoQuery(integrationEvent.OrderId), cancellationToken);

        if (contactResult.IsFailure)
        {
            await _repository.AddAsync(
                NotificationLog.Failed(
                    NotificationChannel.Email, recipient: "unknown", "Payment received", body: string.Empty, now, contactResult.Error.Message),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var contact = contactResult.Value;
        var subject = $"Payment received for order {contact.OrderNumber}";
        var body = $"We received your payment of {integrationEvent.Amount:0.00} {integrationEvent.Currency} for order {contact.OrderNumber}.";

        var sendResult = await _sender.SendEmailAsync(contact.Email, subject, body, cancellationToken);

        var log = sendResult.IsSuccess
            ? NotificationLog.Sent(NotificationChannel.Email, contact.Email, subject, body, now)
            : NotificationLog.Failed(NotificationChannel.Email, contact.Email, subject, body, now, sendResult.Error.Message);

        await _repository.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
