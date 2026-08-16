using Microsoft.Extensions.DependencyInjection;

namespace Persistence.Outbox;

public static class OutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers a background poller for <typeparamref name="TContext"/>'s Outbox table. Call
    /// once per module context that enqueues integration events (currently
    /// <c>OrderingDbContext</c>, <c>PaymentsDbContext</c>) from a composition root that also has
    /// <c>EventBus.AddInProcessEventBus()</c> registered — today that's <c>Store.Worker</c> only.
    /// Adding a new module's outbox to the worker later is one line here, nothing structural.
    /// </summary>
    public static IServiceCollection AddOutboxProcessor<TContext>(
        this IServiceCollection services,
        Action<OutboxProcessingOptions>? configure = null)
        where TContext : AppDbContextBase
    {
        var options = new OutboxProcessingOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddHostedService<OutboxProcessingService<TContext>>();

        return services;
    }
}
