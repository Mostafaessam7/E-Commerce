namespace Infrastructure;

/// <summary>
/// Every timestamp in the system (audit fields, domain event <c>OccurredOnUtc</c>, Outbox
/// <c>OccurredOnUtc</c>/<c>ProcessedAtUtc</c>) goes through this instead of
/// <see cref="DateTime.UtcNow"/> directly, so unit tests can control "now" instead of racing the
/// clock. Storage note: everything is UTC, always — converting to the customer's local time is a
/// presentation concern (Web layer), never something a lower layer decides.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
