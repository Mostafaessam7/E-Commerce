using Microsoft.Extensions.DependencyInjection;

namespace EventBus;

/// <summary>
/// The Phase 10 <see cref="IEventBus"/> implementation promised by the doc comment on that
/// interface: this is a modular monolith on one deployable/one worker, so "publish" means
/// "resolve every <see cref="IIntegrationEventHandler{TEvent}"/> registered for this event type
/// from DI and invoke them in-process" — no broker. Handlers are registered explicitly per module
/// (same rule as everywhere else — ADR-003/004, no assembly scanning); an event with zero
/// registered handlers is not an error, it just means no module currently reacts to it yet.
/// </summary>
internal sealed class InProcessEventBus(IServiceProvider serviceProvider) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        var handlers = serviceProvider.GetServices<IIntegrationEventHandler<TEvent>>();

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
