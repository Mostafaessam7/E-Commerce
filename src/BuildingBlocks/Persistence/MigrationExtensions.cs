using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Persistence;

/// <summary>
/// Applies pending EF Core migrations for <typeparamref name="TContext"/> at startup, retrying a
/// few times before giving up. Local dev never calls this — `dotnet ef database update` against
/// LocalDB is still the normal workflow (docs/database.md); this exists only for the Docker
/// Compose stack (Phase 13), where the app container can start before the SQL Server container has
/// finished accepting connections even though its own health check already passed (the check
/// itself takes a moment; there's no way to be notified the instant it turns healthy). Gated
/// behind an explicit opt-in flag at each composition root's call site
/// (<c>ApplyMigrationsOnStartup</c> config) — never runs by default.
/// </summary>
public static class MigrationExtensions
{
    public static async Task MigrateWithRetryAsync<TContext>(
        this IServiceProvider services,
        ILogger logger,
        int maxAttempts = 10,
        TimeSpan? delayBetweenAttempts = null,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        var delay = delayBetweenAttempts ?? TimeSpan.FromSeconds(3);
        var contextName = typeof(TContext).Name;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Applied migrations for {Context}", contextName);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    ex, "Migration attempt {Attempt}/{MaxAttempts} for {Context} failed, retrying in {Delay}",
                    attempt, maxAttempts, contextName, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
