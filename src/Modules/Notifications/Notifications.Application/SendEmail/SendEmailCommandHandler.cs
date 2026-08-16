using Infrastructure;
using Messaging;
using Notifications.Application.Abstractions;
using Notifications.Contracts;
using Notifications.Domain;
using SharedKernel.Results;

namespace Notifications.Application.SendEmail;

public sealed class SendEmailCommandHandler : IRequestHandler<SendEmailCommand, Unit>
{
    private readonly INotificationSender _sender;
    private readonly INotificationLogRepository _repository;
    private readonly INotificationsUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SendEmailCommandHandler(
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

    public async Task<Result<Unit>> Handle(SendEmailCommand request, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var sendResult = await _sender.SendEmailAsync(request.ToAddress, request.Subject, request.Body, cancellationToken);

        var log = sendResult.IsSuccess
            ? NotificationLog.Sent(NotificationChannel.Email, request.ToAddress, request.Subject, request.Body, now)
            : NotificationLog.Failed(NotificationChannel.Email, request.ToAddress, request.Subject, request.Body, now, sendResult.Error.Message);

        await _repository.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return sendResult.IsSuccess ? Result.Success(Unit.Value) : Result.Failure<Unit>(sendResult.Error);
    }
}
