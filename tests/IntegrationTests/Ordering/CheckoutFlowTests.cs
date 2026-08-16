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
using Security;

namespace IntegrationTests.Ordering;

/// <summary>
/// End-to-end against the real local DB, composing Catalog + Inventory + Ordering exactly like
/// Store.Web's Program.cs does, then driving a full checkout through <see cref="IDispatcher"/> —
/// the same path a real request takes. Proves ADR-014's cross-module dispatch (Ordering calling
/// Catalog for pricing and Inventory for reservation) actually works end to end, not just that
/// each module's own handlers work in isolation.
/// </summary>
public sealed class CheckoutFlowTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";

    private ServiceProvider _provider = null!;
    private Guid _productId;
    private Guid _variantId;
    private Guid _stockItemId;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("ConnectionStrings:Database", ConnectionString)])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSecurityCore();
        services.AddMessagingCore();
        services.AddCatalogModule(configuration);
        services.AddInventoryModule(configuration);
        services.AddOrderingModule(configuration);

        _provider = services.BuildServiceProvider();

        // Seed a real product+variant and matching stock — directly against each module's own
        // DbContext (test code sits outside every module, same as any composition root would).
        using (var scope = _provider.CreateScope())
        {
            var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var product = Product.Create($"Integration Test Product {Guid.NewGuid():N}", $"itest-{Guid.NewGuid():N}", null, null, brandId: null).Value;
            var variantResult = product.AddVariant($"ITEST-{Guid.NewGuid():N}"[..20], 199.99m, "EGP", salePrice: null, barcode: null, weightKg: null);
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
    }

    public async Task DisposeAsync()
    {
        using var scope = _provider.CreateScope();

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

        var orderingDb = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await orderingDb.Carts.Where(c => c.AnonymousId != null).ExecuteDeleteAsync();
        await orderingDb.Orders.Where(o => o.Notes == TestNotesMarker).ExecuteDeleteAsync();

        await _provider.DisposeAsync();
    }

    private const string TestNotesMarker = "integration-test-checkout";

    [Fact]
    public async Task Placing_an_order_reserves_stock_and_creates_the_order_with_correct_totals()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var anonymousId = Guid.NewGuid();
        var cartResult = await dispatcher.Send(new GetOrCreateCartCommand(CustomerId: null, AnonymousId: anonymousId));
        cartResult.IsSuccess.Should().BeTrue();

        var addResult = await dispatcher.Send(new AddCartItemCommand(cartResult.Value.Id, _variantId, Quantity: 2));
        addResult.IsSuccess.Should().BeTrue();
        addResult.Value.Items.Should().ContainSingle(i => i.ProductVariantId == _variantId && i.Quantity == 2);

        var address = new AddressInput("Ahmed Ali", "+201000000000", "1 Test St", null, "Cairo", null, "11511", "EG");
        var placeResult = await dispatcher.Send(new PlaceOrderCommand(
            cartResult.Value.Id, CustomerId: null, Email: "buyer@example.com", address, address, ShippingCost: 30m, Notes: TestNotesMarker));

        placeResult.IsSuccess.Should().BeTrue();

        var orderResult = await dispatcher.Send(new GetOrderQuery(placeResult.Value));
        orderResult.IsSuccess.Should().BeTrue();
        orderResult.Value.Subtotal.Should().Be(199.99m * 2);
        orderResult.Value.ShippingCost.Should().Be(30m);
        orderResult.Value.Status.Should().Be("Pending");
        orderResult.Value.PaymentStatus.Should().Be("Pending");

        // Stock was actually reserved, not just "would have been".
        var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var stock = await inventoryDb.StockItems.AsNoTracking().FirstAsync(s => s.Id == _stockItemId);
        stock.QuantityReserved.Should().Be(2);

        // The cart was cleared after checkout.
        var cartAfter = await dispatcher.Send(new GetCartQuery(cartResult.Value.Id));
        cartAfter.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Placing_an_order_for_more_than_available_stock_fails_and_reserves_nothing()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var anonymousId = Guid.NewGuid();
        var cartResult = await dispatcher.Send(new GetOrCreateCartCommand(CustomerId: null, AnonymousId: anonymousId));
        await dispatcher.Send(new AddCartItemCommand(cartResult.Value.Id, _variantId, Quantity: 999));

        var address = new AddressInput("Ahmed Ali", "+201000000000", "1 Test St", null, "Cairo", null, "11511", "EG");
        var placeResult = await dispatcher.Send(new PlaceOrderCommand(
            cartResult.Value.Id, CustomerId: null, Email: "buyer@example.com", address, address, ShippingCost: 30m, Notes: TestNotesMarker));

        placeResult.IsFailure.Should().BeTrue();
        placeResult.Error.Code.Should().Be("StockItem.InsufficientStock");

        var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var stock = await inventoryDb.StockItems.AsNoTracking().FirstAsync(s => s.Id == _stockItemId);
        stock.QuantityReserved.Should().Be(0, "a failed checkout must not leave a partial reservation behind");
    }
}
