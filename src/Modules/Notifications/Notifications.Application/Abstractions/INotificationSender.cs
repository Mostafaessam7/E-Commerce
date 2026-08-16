using SharedKernel.Results;

namespace Notifications.Application.Abstractions;

/// <summary>
/// The only thing this module's handlers know about "sending an email" — no real provider account
/// exists (same shape as Payments' <c>IPaymentGateway</c>/<c>FakePaymentGateway</c>), so
/// <c>FakeEmailSender</c> is the only implementation: it logs instead of calling a real SMTP/API
/// provider, but every call still goes through this interface and gets recorded in
/// <see cref="Notifications.Domain.NotificationLog"/>. Swapping in a real provider (SendGrid, SES)
/// means adding one new class, not touching any handler.
/// </summary>
public interface INotificationSender
{
    Task<Result> SendEmailAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default);
}
