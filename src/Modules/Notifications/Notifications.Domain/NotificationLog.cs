using SharedKernel.Auditing;
using SharedKernel.Guards;

namespace Notifications.Domain;

public enum NotificationChannel
{
    Email,
}

public enum NotificationStatus
{
    Sent,
    Failed,
}

/// <summary>
/// A record of one notification attempt — not an aggregate root with business rules, just an
/// append-only audit trail of what was sent, to whom, and whether it worked. Owned entirely by
/// this module; nothing else ever reads or writes it directly (Section: notifications react to
/// other modules' integration events, they don't expose write APIs to them).
/// </summary>
public sealed class NotificationLog : AuditableEntity<Guid>
{
    private NotificationLog(
        Guid id, NotificationChannel channel, string recipient, string subject, string body,
        NotificationStatus status, DateTime sentAtUtc, string? error)
        : base(id)
    {
        Channel = channel;
        Recipient = recipient;
        Subject = subject;
        Body = body;
        Status = status;
        SentAtUtc = sentAtUtc;
        Error = error;
    }

    private NotificationLog()
    {
    }

    public NotificationChannel Channel { get; private set; }

    public string Recipient { get; private set; } = null!;

    public string Subject { get; private set; } = null!;

    public string Body { get; private set; } = null!;

    public NotificationStatus Status { get; private set; }

    public DateTime SentAtUtc { get; private set; }

    public string? Error { get; private set; }

    public static NotificationLog Sent(NotificationChannel channel, string recipient, string subject, string body, DateTime sentAtUtc)
    {
        Guard.Against.NullOrWhiteSpace(recipient, nameof(recipient));
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));

        return new NotificationLog(Guid.NewGuid(), channel, recipient, subject, body, NotificationStatus.Sent, sentAtUtc, error: null);
    }

    public static NotificationLog Failed(NotificationChannel channel, string recipient, string subject, string body, DateTime sentAtUtc, string error)
    {
        Guard.Against.NullOrWhiteSpace(recipient, nameof(recipient));
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));

        return new NotificationLog(Guid.NewGuid(), channel, recipient, subject, body, NotificationStatus.Failed, sentAtUtc, error);
    }
}
