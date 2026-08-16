using System.Reflection;
using System.Text.Json;
using EventBus;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Persistence.Outbox;

/// <summary>
/// Polls one module's <c>OutboxMessages</c> table and dispatches unprocessed rows through
/// <see cref="IEventBus"/>. One instance per module context (registered via
/// <see cref="OutboxServiceCollectionExtensions.AddOutboxProcessor{TContext}"/>) — each module's
/// Outbox is independent (ADR-005: one DbContext per module, no shared table), so there is
/// deliberately no single cross-module processor.
///
/// At-least-once delivery: a row is marked processed only after every registered handler for its
/// event type has returned without throwing (see docs/events.md — handlers must be idempotent). A
/// row that fails to dispatch is marked with <see cref="OutboxMessage.MarkFailed"/> (not deleted,
/// not skipped) and retried on the next poll, since <see cref="OutboxMessage"/> exposes no way to
/// tell "failed" from "not yet attempted" other than the <c>Error</c> text — that's intentional:
/// a transient failure (e.g. the DB was briefly unreachable) should self-heal on retry, and a
/// permanent one (bad payload) is visible via <c>Error</c>/logs for a human to investigate,
/// without silently dropping the message either way.
/// </summary>
internal sealed class OutboxProcessingService<TContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessingService<TContext>> logger,
    OutboxProcessingOptions options) : BackgroundService
    where TContext : AppDbContextBase
{
    private static readonly MethodInfo PublishMethod =
        typeof(IEventBus).GetMethod(nameof(IEventBus.PublishAsync))
        ?? throw new MissingMethodException(nameof(IEventBus), nameof(IEventBus.PublishAsync));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox batch failed for {Context}", typeof(TContext).Name);
            }

            try
            {
                await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(options.BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            await DispatchAsync(message, eventBus, dateTimeProvider, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchAsync(
        OutboxMessage message,
        IEventBus eventBus,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            var eventType = ResolveEventType(message.Type);
            if (eventType is null)
            {
                var error = $"Unresolvable integration event type '{message.Type}' — no loaded assembly declares it.";
                logger.LogWarning("Outbox message {MessageId}: {Error}", message.Id, error);
                message.MarkFailed(error);
                return;
            }

            var integrationEvent = JsonSerializer.Deserialize(message.Content, eventType);
            if (integrationEvent is null)
            {
                message.MarkFailed("Deserialization produced null.");
                return;
            }

            // IEventBus.PublishAsync<TEvent> is generic; the event's concrete type is only known
            // at runtime here (it comes from the stored Type string), so it's invoked via
            // reflection rather than a compile-time generic call — the one place in this codebase
            // that's necessary, confined to this class.
            var publishTask = (Task)PublishMethod.MakeGenericMethod(eventType)
                .Invoke(eventBus, [integrationEvent, cancellationToken])!;
            await publishTask.ConfigureAwait(false);

            message.MarkProcessed(dateTimeProvider.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outbox message {MessageId} ({Type}) failed to dispatch", message.Id, message.Type);
            message.MarkFailed(ex.Message);
        }
    }

    /// <summary>
    /// <see cref="AppDbContextBase.EnqueueOutboxMessage"/> stores the assembly-qualified type
    /// name specifically so this can use <see cref="Type.GetType(string,bool)"/> — unlike
    /// scanning already-loaded assemblies for a bare <c>FullName</c>, this form makes the CLR
    /// load the declaring assembly itself if it isn't already loaded in this process. A ProjectReference
    /// alone does not guarantee an assembly is loaded — .NET loads assemblies lazily on first
    /// use, and a *.Contracts assembly whose only real usage is inside a handler method the
    /// worker never happens to execute (e.g. one that only runs when Store.Web calls it) may
    /// never get JIT-loaded otherwise.
    /// </summary>
    private static Type? ResolveEventType(string typeName)
    {
        var type = Type.GetType(typeName, throwOnError: false);
        if (type is not null)
        {
            return type;
        }

        // Fallback for any pre-existing row stored under the old FullName-only format.
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName))
            .FirstOrDefault(t => t is not null);
    }
}
