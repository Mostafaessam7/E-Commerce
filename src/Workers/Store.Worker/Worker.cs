namespace Store.Worker;

/// <summary>
/// Placeholder host. Phase 10 replaces this with the real Outbox-processing background service
/// (poll unpublished OutboxMessages, dispatch through IEventBus, mark ProcessedAtUtc) — kept as
/// a trivially-running worker for now so `dotnet run` on Store.Worker proves the host, DI
/// container, and BuildingBlocks references are wired correctly end to end.
/// </summary>
public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker heartbeat at: {UtcNow}", DateTimeOffset.UtcNow);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
