using EventBus;
using Infrastructure;
using Notifications.Application.Abstractions;
using Notifications.Domain;
using Ordering.Contracts;

namespace Notifications.Application.OrderConfirmation;

/// <summary>
/// Reacts to <see cref="OrderPlacedIntegrationEvent"/> — order confirmation email. Idempotent by
/// construction: sending the same confirmation twice for an at-least-once redelivery just writes
/// a second <see cref="NotificationLog"/> row, which is harmless (unlike double-charging a
/// payment) — no dedupe ledger needed here the way Payments' webhook handler needs one.
/// </summary>
public sealed class OrderPlacedNotificationHandler : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    private readonly INotificationSender _sender;
    private readonly INotificationLogRepository _repository;
    private readonly INotificationsUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public OrderPlacedNotificationHandler(
        INotificationSender sender,
        INotificationLogRepository repository,
        INotificationsUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _sender = sender;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task HandleAsync(OrderPlacedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var subject = $"Order {integrationEvent.OrderNumber} confirmed";
        var body = $"Thanks for your order! {integrationEvent.OrderNumber} totals {integrationEvent.Total:0.00} {integrationEvent.Currency}.";

        var sendResult = await _sender.SendEmailAsync(integrationEvent.Email, subject, body, cancellationToken);
        var now = _dateTimeProvider.UtcNow;

        var log = sendResult.IsSuccess
            ? NotificationLog.Sent(NotificationChannel.Email, integrationEvent.Email, subject, body, now)
            : NotificationLog.Failed(NotificationChannel.Email, integrationEvent.Email, subject, body, now, sendResult.Error.Message);

        await _repository.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
