namespace Persistence.Outbox;

/// <summary>
/// A row-per-integration-event written in the same DB transaction as the change that caused it
/// (see docs/events.md). <see cref="Type"/> is the CLR full type name of the integration event,
/// <see cref="Content"/> is that event JSON-serialized — <c>Store.Worker</c>'s Phase 10 processor
/// deserializes by <see cref="Type"/> and dispatches through <c>EventBus.IEventBus</c>.
/// Every module's <see cref="AppDbContextBase"/> gets an identical OutboxMessages table; there is
/// no single shared outbox across modules (each module owns its own DbContext — ADR-005).
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(Guid id, string type, string content, DateTime occurredOnUtc)
    {
        Id = id;
        Type = type;
        Content = content;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = null!;

    public string Content { get; private set; } = null!;

    public DateTime OccurredOnUtc { get; private set; }

    public DateTime? ProcessedOnUtc { get; private set; }

    public string? Error { get; private set; }

    public void MarkProcessed(DateTime processedOnUtc) => ProcessedOnUtc = processedOnUtc;

    public void MarkFailed(string error) => Error = error;
}
