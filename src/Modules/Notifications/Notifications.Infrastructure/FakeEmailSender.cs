using Microsoft.Extensions.Logging;
using Notifications.Application.Abstractions;
using SharedKernel.Results;

namespace Notifications.Infrastructure;

/// <summary>
/// No real email provider account exists (same shape as Payments' <c>FakePaymentGateway</c>) —
/// this just logs instead of calling SendGrid/SES/SMTP. Every handler still goes through
/// <see cref="INotificationSender"/> and every call still gets a real <c>NotificationLog</c> row
/// (the handlers write that, not this class) — swapping in a real provider is one new class.
/// </summary>
internal sealed class FakeEmailSender : INotificationSender
{
    private readonly ILogger<FakeEmailSender> _logger;

    public FakeEmailSender(ILogger<FakeEmailSender> logger) => _logger = logger;

    public Task<Result> SendEmailAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fake email sent to {ToAddress}: {Subject}", toAddress, subject);
        return Task.FromResult(Result.Success());
    }
}
