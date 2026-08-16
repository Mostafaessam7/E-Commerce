using Microsoft.Extensions.DependencyInjection;

namespace EventBus;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="InProcessEventBus"/> as <see cref="IEventBus"/>. Call once from a
    /// composition root that also processes an Outbox (currently <c>Store.Worker</c> only —
    /// nothing else publishes through <see cref="IEventBus"/> directly).
    /// </summary>
    public static IServiceCollection AddInProcessEventBus(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, InProcessEventBus>();
        return services;
    }
}
