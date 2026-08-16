using Catalog.Domain;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using EventBus;
using FluentAssertions;
using Infrastructure;
using Inventory.Domain;
using Inventory.Infrastructure;
using Inventory.Infrastructure.Persistence;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Domain;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Ordering.Application.Carts;
using Ordering.Application.Checkout;
using Ordering.Contracts;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Persistence;
using Payments.Application.Abstractions;
using Payments.Application.Payments;
using Payments.Contracts;
using Payments.Infrastructure;
using Payments.Infrastructure.Persistence;
using Security;

namespace IntegrationTests.Notifications;

/// <summary>
/// Proves Notifications' two integration event handlers actually run and write a
/// <see cref="NotificationLog"/> row — not just that they're registered. Composes the same
/// modules Store.Worker does (Ordering + Payments + Notifications + InProcessEventBus), then calls
/// <see cref="IEventBus.PublishAsync{TEvent}"/> directly instead of going through the Outbox
/// processor (that plumbing is already proven by OutboxProcessingServiceTests) — this test is
/// about the handlers' own logic, not the delivery mechanism.
/// </summary>
public sealed class NotificationHandlersTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";
    private const string WebhookSecret = "notification-test-webhook-secret";

    private ServiceProvider _provider = null!;
    private Guid _productId;
    private Guid _variantId;
    private Guid _stockItemId;
    private Guid _orderId;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("ConnectionStrings:Database", ConnectionString),
                new("Payments:WebhookSecret", WebhookSecret),
            ])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSecurityCore();
        services.AddMessagingCore();
        services.AddInProcessEventBus();
        services.AddCatalogModule(configuration);
        services.AddInventoryModule(configuration);
        services.AddOrderingModule(configuration);
        services.AddPaymentsModule(configuration);
        services.AddNotificationsModule(configuration);

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var product = Product.Create($"Notification Test Product {Guid.NewGuid():N}", $"notif-test-{Guid.NewGuid():N}", null, null, brandId: null).Value;
        var variantResult = product.AddVariant($"NOTIF-{Guid.NewGuid():N}"[..20], 80m, "EGP", salePrice: null, barcode: null, weightKg: null);
        product.Publish();
        catalogDb.Products.Add(product);
        await catalogDb.SaveChangesAsync();
        _productId = product.Id;
        _variantId = variantResult.Value;

        var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var stockItem = StockItem.Create(_variantId, initialQuantity: 5).Value;
        inventoryDb.StockItems.Add(stockItem);
        await inventoryDb.SaveChangesAsync();
        _stockItemId = stockItem.Id;
    }

    public async Task DisposeAsync()
    {
        using var scope = _provider.CreateScope();

        var notificationsDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await notificationsDb.NotificationLogs.ExecuteDeleteAsync();

        var paymentsDb = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        await paymentsDb.PaymentTransactions.Where(p => p.OrderId == _orderId).ExecuteDeleteAsync();
        await paymentsDb.ProcessedWebhookEvents.ExecuteDeleteAsync();

        var orderingDb = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await orderingDb.Orders.Where(o => o.Id == _orderId).ExecuteDeleteAsync();
        await orderingDb.Carts.Where(c => c.AnonymousId != null).ExecuteDeleteAsync();

        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var product = await catalogDb.Products.FindAsync(_productId);
        if (product is not null)
        {
            catalogDb.Products.Remove(product);
            await catalogDb.SaveChangesAsync();
        }

        var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var stockItem = await inventoryDb.StockItems.FindAsync(_stockItemId);
        if (stockItem is not null)
        {
            inventoryDb.StockItems.Remove(stockItem);
            await inventoryDb.SaveChangesAsync();
        }

        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task OrderPlaced_event_writes_a_sent_notification_log_using_the_order_email()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        _orderId = await PlaceTestOrderAsync(dispatcher, "confirm-me@example.com");
        var order = (await dispatcher.Send(new GetOrderQuery(_orderId))).Value;

        await eventBus.PublishAsync(new OrderPlacedIntegrationEvent(order.Id, order.OrderNumber, null, order.Email, order.Total, order.Currency));

        var notificationsDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var log = await notificationsDb.NotificationLogs
            .OrderByDescending(n => n.SentAtUtc)
            .FirstAsync(n => n.Recipient == "confirm-me@example.com");

        log.Status.Should().Be(NotificationStatus.Sent);
        log.Subject.Should().Contain(order.OrderNumber);
    }

    [Fact]
    public async Task PaymentSucceeded_event_looks_up_the_order_email_via_dispatch_and_writes_a_sent_log()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        _orderId = await PlaceTestOrderAsync(dispatcher, "receipt-me@example.com");

        var initResult = await dispatcher.Send(new InitializePaymentCommand(_orderId, 80m, "EGP"));
        var simulator = scope.ServiceProvider.GetRequiredService<IWebhookSimulator>();
        var (payload, signature) = simulator.BuildSucceededPayload(initResult.Value.PaymentTransactionId);
        var webhookResult = await dispatcher.Send(new ProcessWebhookCommand(payload, signature));
        webhookResult.IsSuccess.Should().BeTrue();

        // ProcessWebhookCommand already enqueues PaymentSucceededIntegrationEvent to the Outbox
        // (proven by OutboxProcessingServiceTests) — publish it directly here too, same as the
        // OrderPlaced test above, to isolate this test to the handler's own logic.
        await eventBus.PublishAsync(new PaymentSucceededIntegrationEvent(
            initResult.Value.PaymentTransactionId, _orderId, 80m, "EGP"));

        var notificationsDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var log = await notificationsDb.NotificationLogs
            .OrderByDescending(n => n.SentAtUtc)
            .FirstAsync(n => n.Recipient == "receipt-me@example.com");

        log.Status.Should().Be(NotificationStatus.Sent);
        log.Subject.Should().Contain("Payment received");
    }

    private async Task<Guid> PlaceTestOrderAsync(IDispatcher dispatcher, string email)
    {
        var anonymousId = Guid.NewGuid();
        var cart = (await dispatcher.Send(new GetOrCreateCartCommand(null, anonymousId))).Value;
        await dispatcher.Send(new AddCartItemCommand(cart.Id, _variantId, 1));

        var address = new AddressInput("Notif Test", "+201000000003", "1 Test St", null, "Cairo", null, "11511", "EG");
        var placeResult = await dispatcher.Send(new PlaceOrderCommand(cart.Id, null, email, address, address, 10m, "notification-test"));
        return placeResult.Value;
    }
}
