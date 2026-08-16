namespace Persistence.Outbox;

/// <summary>Tuning for one module's <see cref="OutboxProcessingService{TContext}"/> instance.</summary>
public sealed class OutboxProcessingOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);

    public int BatchSize { get; set; } = 20;
}
