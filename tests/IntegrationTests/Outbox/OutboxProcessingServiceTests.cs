using EventBus;
using FluentAssertions;
using Infrastructure;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ordering.Contracts;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Persistence;
using Persistence.Outbox;
using Security;

namespace IntegrationTests.Outbox;

/// <summary>
/// Proves the Phase 10 processor against the real DB: a row written straight to
/// <c>OrderingDbContext.OutboxMessages</c> (the exact mechanism <c>PlaceOrderCommandHandler</c>
/// uses) gets picked up, deserialized, dispatched to a real registered handler, and marked
/// processed — without going through Store.Worker's actual host, just its DI wiring
/// (<c>AddOutboxProcessor</c>/<c>AddInProcessEventBus</c>).
/// </summary>
public sealed class OutboxProcessingServiceTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";

    private ServiceProvider _provider = null!;
    private RecordingHandler _handler = null!;
    private Guid _messageId;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("ConnectionStrings:Database", ConnectionString)])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSecurityCore();
        services.AddMessagingCore();
        services.AddOrderingModule(configuration);
        services.AddInProcessEventBus();

        _handler = new RecordingHandler();
        services.AddSingleton(_handler);
        services.AddSingleton<IIntegrationEventHandler<OrderPlacedIntegrationEvent>>(_handler);

        services.AddOutboxProcessor<OrderingDbContext>(options =>
        {
            options.PollInterval = TimeSpan.FromMilliseconds(200);
            options.BatchSize = 10;
        });

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();

        var integrationEvent = new OrderPlacedIntegrationEvent(Guid.NewGuid(), "OUTBOX-TEST", null, 10m, "EGP");
        _messageId = integrationEvent.EventId;
        db.EnqueueIntegrationEvent(integrationEvent);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await db.OutboxMessages.Where(m => m.Id == _messageId).ExecuteDeleteAsync();

        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Processor_dispatches_a_pending_row_to_its_registered_handler_and_marks_it_processed()
    {
        var hostedService = _provider.GetServices<IHostedService>()
            .Single(s => s.GetType().Name.StartsWith("OutboxProcessingService", StringComparison.Ordinal));

        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline && _handler.Handled.Count == 0)
            {
                await Task.Delay(100);
            }
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        _handler.Handled.Should().ContainSingle(e => e.EventId == _messageId);

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var message = await db.OutboxMessages.SingleAsync(m => m.Id == _messageId);
        message.ProcessedOnUtc.Should().NotBeNull();
        message.Error.Should().BeNull();
    }

    private sealed class RecordingHandler : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
    {
        public List<OrderPlacedIntegrationEvent> Handled { get; } = [];

        public Task HandleAsync(OrderPlacedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            Handled.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }
}
