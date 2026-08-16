using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Store.Worker;

/// <summary>
/// Store.Worker has no inbound HTTP listener (plain <c>Microsoft.NET.Sdk.Worker</c> host, unlike
/// Store.Web) so there's no <c>/health</c> endpoint to expose here — <see cref="IHealthCheckPublisher"/>
/// is the generic-host equivalent: the health check subsystem runs the same checks
/// (<c>AddDbContextCheck&lt;T&gt;</c>, registered in Program.cs) on a timer and hands the result
/// to whatever's registered here instead of an HTTP client polling an endpoint. Logs are the
/// simplest sink that needs no new infrastructure; swap for a real publisher (a monitoring
/// system's push API) if one is ever wired up.
/// </summary>
public sealed class LoggingHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly ILogger<LoggingHealthCheckPublisher> _logger;

    public LoggingHealthCheckPublisher(ILogger<LoggingHealthCheckPublisher> logger) => _logger = logger;

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        if (report.Status == HealthStatus.Healthy)
        {
            _logger.LogInformation("Health check: {Status} ({Duration}ms)", report.Status, report.TotalDuration.TotalMilliseconds);
        }
        else
        {
            foreach (var entry in report.Entries.Where(e => e.Value.Status != HealthStatus.Healthy))
            {
                _logger.LogWarning(
                    entry.Value.Exception, "Health check '{Name}' reported {Status}: {Description}",
                    entry.Key, entry.Value.Status, entry.Value.Description);
            }
        }

        return Task.CompletedTask;
    }
}
