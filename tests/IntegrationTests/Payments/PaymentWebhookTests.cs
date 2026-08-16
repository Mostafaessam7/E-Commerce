using Catalog.Domain;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using FluentAssertions;
using Infrastructure;
using Inventory.Domain;
using Inventory.Infrastructure;
using Inventory.Infrastructure.Persistence;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Carts;
using Ordering.Application.Checkout;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Persistence;
using Payments.Application.Abstractions;
using Payments.Application.Payments;
using Payments.Infrastructure;
using Payments.Infrastructure.Persistence;
using Security;

namespace IntegrationTests.Payments;

/// <summary>
/// End-to-end against the real local DB, composing Catalog + Inventory + Ordering + Payments
/// exactly like Store.Web's Program.cs. Proves Section 9's requirements aren't just present in
/// code but actually enforced: an invalid signature is rejected before anything else happens, a
/// redelivered webhook is a no-op the second time, and a successful one really does flip the
/// Order's PaymentStatus via the reverse cross-module dispatch (Payments -> Ordering).
/// </summary>
public sealed class PaymentWebhookTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";
    private const string WebhookSecret = "integration-test-webhook-secret";
    private const string TestNotesMarker = "integration-test-payment-webhook";

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
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSecurityCore();
        services.AddMessagingCore();
        services.AddCatalogModule(configuration);
        services.AddInventoryModule(configuration);
        services.AddOrderingModule(configuration);
        services.AddPaymentsModule(configuration);

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();

        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var product = Product.Create($"Payment Test Product {Guid.NewGuid():N}", $"ptest-{Guid.NewGuid():N}", null, null, brandId: null).Value;
        var variantResult = product.AddVariant($"PTEST-{Guid.NewGuid():N}"[..20], 250m, "EGP", salePrice: null, barcode: null, weightKg: null);
        product.Publish();
        catalogDb.Products.Add(product);
        await catalogDb.SaveChangesAsync();
        _productId = product.Id;
        _variantId = variantResult.Value;

        var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var stockItem = StockItem.Create(_variantId, initialQuantity: 10).Value;
        inventoryDb.StockItems.Add(stockItem);
        await inventoryDb.SaveChangesAsync();
        _stockItemId = stockItem.Id;

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var anonymousId = Guid.NewGuid();
        var cart = (await dispatcher.Send(new GetOrCreateCartCommand(null, anonymousId))).Value;
        await dispatcher.Send(new AddCartItemCommand(cart.Id, _variantId, 1));
        var address = new AddressInput("Sara Adel", "+201000000001", "5 Test St", null, "Giza", null, "12511", "EG");
        var placeResult = await dispatcher.Send(new PlaceOrderCommand(cart.Id, null, address, address, 20m, TestNotesMarker));
        _orderId = placeResult.Value;
    }

    public async Task DisposeAsync()
    {
        using var scope = _provider.CreateScope();

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
    public async Task Webhook_with_an_invalid_signature_is_rejected_before_any_processing()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var initResult = await dispatcher.Send(new InitializePaymentCommand(_orderId, 270m, "EGP"));

        var result = await dispatcher.Send(new ProcessWebhookCommand(
            $$"""{"eventId":"evt_fake","eventType":"payment.succeeded","paymentTransactionId":"{{initResult.Value.PaymentTransactionId}}"}""",
            "not-a-real-signature"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Webhook.InvalidSignature");

        var paymentResult = await dispatcher.Send(new GetPaymentQuery(initResult.Value.PaymentTransactionId));
        paymentResult.Value.Status.Should().Be("Pending", "a rejected webhook must not change payment state");
    }

    [Fact]
    public async Task Successful_payment_webhook_marks_the_payment_succeeded_and_the_order_paid()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var simulator = scope.ServiceProvider.GetRequiredService<IWebhookSimulator>();

        var initResult = await dispatcher.Send(new InitializePaymentCommand(_orderId, 270m, "EGP"));
        var (payload, signature) = simulator.BuildSucceededPayload(initResult.Value.PaymentTransactionId);

        var webhookResult = await dispatcher.Send(new ProcessWebhookCommand(payload, signature));
        webhookResult.IsSuccess.Should().BeTrue();

        var payment = (await dispatcher.Send(new GetPaymentQuery(initResult.Value.PaymentTransactionId))).Value;
        payment.Status.Should().Be("Succeeded");

        var order = (await dispatcher.Send(new GetOrderQuery(_orderId))).Value;
        order.PaymentStatus.Should().Be("Paid", "the webhook handler must dispatch MarkOrderAsPaidCommand into Ordering");
        order.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task Redelivering_the_same_webhook_event_id_is_idempotent()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var simulator = scope.ServiceProvider.GetRequiredService<IWebhookSimulator>();

        var initResult = await dispatcher.Send(new InitializePaymentCommand(_orderId, 270m, "EGP"));
        var (payload, signature) = simulator.BuildSucceededPayload(initResult.Value.PaymentTransactionId);

        var first = await dispatcher.Send(new ProcessWebhookCommand(payload, signature));
        var second = await dispatcher.Send(new ProcessWebhookCommand(payload, signature));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue("a duplicate delivery is acknowledged, not treated as an error");

        var payment = (await dispatcher.Send(new GetPaymentQuery(initResult.Value.PaymentTransactionId))).Value;
        payment.Status.Should().Be("Succeeded", "reprocessing must not be attempted, let alone change the outcome");
    }
}
