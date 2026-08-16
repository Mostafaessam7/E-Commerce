using Catalog.Application.Products;
using Catalog.Domain;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using FluentAssertions;
using Infrastructure;
using Inventory.Application.Stock;
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

namespace IntegrationTests.Admin;

/// <summary>
/// Phase 11's admin commands/queries against the real DB — proves the wrappers around each
/// aggregate's existing domain methods (Product.Publish/Archive/Delete, Order's status
/// transitions, StockItem.AdjustTo) actually persist, and that a deleted product really
/// disappears from the write-side repository (the global soft-delete query filter, not new code
/// here — see docs/database.md).
/// </summary>
public sealed class AdminOperationsTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";

    private ServiceProvider _provider = null!;
    private Guid _productId;
    private Guid _sellableProductId;
    private Guid _stockVariantId;
    private Guid _stockItemId;
    private Guid _orderId;

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

        using var scope = _provider.CreateScope();

        // PlaceOrderCommand re-validates every line against Catalog (ADR-014), so the "sellable"
        // stock item used by the order-status tests needs a real backing Product/variant — not
        // just an arbitrary Guid — same seeding shape as PaymentWebhookTests.
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var product = Product.Create($"Admin Ops Test Product {Guid.NewGuid():N}", $"admin-ops-{Guid.NewGuid():N}", null, null, brandId: null).Value;
        var variantResult = product.AddVariant($"AOT-{Guid.NewGuid():N}"[..20], 150m, "EGP", salePrice: null, barcode: null, weightKg: null);
        product.Publish();
        catalogDb.Products.Add(product);
        await catalogDb.SaveChangesAsync();
        _sellableProductId = product.Id;
        _stockVariantId = variantResult.Value;

        var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var stockItem = StockItem.Create(_stockVariantId, initialQuantity: 5).Value;
        inventoryDb.StockItems.Add(stockItem);
        await inventoryDb.SaveChangesAsync();
        _stockItemId = stockItem.Id;
    }

    public async Task DisposeAsync()
    {
        using var scope = _provider.CreateScope();

        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await catalogDb.Products.IgnoreQueryFilters()
            .Where(p => p.Id == _productId || p.Id == _sellableProductId)
            .ExecuteDeleteAsync();

        var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await inventoryDb.StockItems.Where(s => s.Id == _stockItemId).ExecuteDeleteAsync();

        if (_orderId != Guid.Empty)
        {
            var orderingDb = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            await orderingDb.Orders.Where(o => o.Id == _orderId).ExecuteDeleteAsync();
            await orderingDb.Carts.Where(c => c.AnonymousId != null).ExecuteDeleteAsync();
        }

        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Product_lifecycle_create_add_variant_publish_archive_delete()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var createResult = await dispatcher.Send(new CreateProductCommand(
            $"Admin Test Product {Guid.NewGuid():N}", $"admin-test-{Guid.NewGuid():N}", null, null, BrandId: null, CategoryIds: null));
        createResult.IsSuccess.Should().BeTrue();
        _productId = createResult.Value;

        var updateResult = await dispatcher.Send(new UpdateProductCommand(_productId, "Renamed Product", "short", "long"));
        updateResult.IsSuccess.Should().BeTrue();

        // Publishing without a variant must fail (Product.Publish's own guard) — proves the
        // admin command surfaces the domain rule instead of swallowing it.
        var publishBeforeVariant = await dispatcher.Send(new PublishProductCommand(_productId));
        publishBeforeVariant.IsFailure.Should().BeTrue();
        publishBeforeVariant.Error.Code.Should().Be("Product.NoVariants");

        var variantResult = await dispatcher.Send(new AddProductVariantCommand(_productId, $"SKU-{Guid.NewGuid():N}"[..12], 99.99m, "EGP", null));
        variantResult.IsSuccess.Should().BeTrue();

        var publishResult = await dispatcher.Send(new PublishProductCommand(_productId));
        publishResult.IsSuccess.Should().BeTrue();

        var afterPublish = (await dispatcher.Send(new GetProductByIdQuery(_productId))).Value;
        afterPublish.Name.Should().Be("Renamed Product");
        afterPublish.Status.Should().Be("Active");
        afterPublish.Variants.Should().ContainSingle();

        var archiveResult = await dispatcher.Send(new ArchiveProductCommand(_productId));
        archiveResult.IsSuccess.Should().BeTrue();
        (await dispatcher.Send(new GetProductByIdQuery(_productId))).Value.Status.Should().Be("Archived");

        var deleteResult = await dispatcher.Send(new DeleteProductCommand(_productId));
        deleteResult.IsSuccess.Should().BeTrue();

        var afterDelete = await dispatcher.Send(new GetProductByIdQuery(_productId));
        afterDelete.IsFailure.Should().BeTrue("the global soft-delete query filter must exclude it from the write-side repository too");
    }

    [Fact]
    public async Task Admin_product_search_includes_all_statuses_only_when_requested()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var createResult = await dispatcher.Send(new CreateProductCommand(
            $"Draft Only Product {Guid.NewGuid():N}", $"draft-only-{Guid.NewGuid():N}", null, null, BrandId: null, CategoryIds: null));
        _productId = createResult.Value;

        var storefrontSearch = await dispatcher.Send(
            new SearchProductsQuery(new ProductSearchCriteria(SearchTerm: "Draft Only Product", Page: 1, PageSize: 10)));
        storefrontSearch.Value.Items.Should().BeEmpty("a Draft product must not appear in the storefront's Active-only search");

        var adminSearch = await dispatcher.Send(
            new SearchProductsQuery(new ProductSearchCriteria(SearchTerm: "Draft Only Product", Page: 1, PageSize: 10, IncludeAllStatuses: true)));
        adminSearch.Value.Items.Should().ContainSingle(p => p.Id == _productId && p.Status == "Draft");
    }

    [Fact]
    public async Task Order_status_can_be_walked_through_confirm_process_ship_deliver()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        _orderId = await PlaceTestOrderAsync(dispatcher);

        (await dispatcher.Send(new ConfirmOrderCommand(_orderId))).IsSuccess.Should().BeTrue();
        (await dispatcher.Send(new StartProcessingOrderCommand(_orderId))).IsSuccess.Should().BeTrue();
        (await dispatcher.Send(new ShipOrderCommand(_orderId, "TRACK-123"))).IsSuccess.Should().BeTrue();
        (await dispatcher.Send(new DeliverOrderCommand(_orderId))).IsSuccess.Should().BeTrue();

        var order = (await dispatcher.Send(new GetOrderQuery(_orderId))).Value;
        order.Status.Should().Be("Delivered");

        // Illegal transition from a terminal state surfaces as a Result.Failure, not an exception.
        var illegal = await dispatcher.Send(new CancelOrderCommand(_orderId, "too late"));
        illegal.IsFailure.Should().BeTrue();
        illegal.Error.Code.Should().Be("Order.CannotCancel");
    }

    [Fact]
    public async Task Order_appears_in_admin_search_and_can_be_cancelled()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        _orderId = await PlaceTestOrderAsync(dispatcher);

        var cancelResult = await dispatcher.Send(new CancelOrderCommand(_orderId, "customer requested"));
        cancelResult.IsSuccess.Should().BeTrue();

        var search = await dispatcher.Send(new SearchOrdersQuery(new OrderSearchCriteria(Status: "Cancelled", Page: 1, PageSize: 50)));
        search.Value.Items.Should().Contain(o => o.Id == _orderId);
    }

    [Fact]
    public async Task Stock_can_be_adjusted_to_a_known_quantity_with_a_reason()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var adjustResult = await dispatcher.Send(new AdjustStockCommand(_stockVariantId, 42, "Warehouse recount"));
        adjustResult.IsSuccess.Should().BeTrue();

        var stock = (await dispatcher.Send(new GetStockQuery(_stockVariantId))).Value;
        stock.QuantityOnHand.Should().Be(42);

        var search = await dispatcher.Send(new SearchStockQuery(Page: 1, PageSize: 200));
        search.Value.Items.Should().Contain(s => s.ProductVariantId == _stockVariantId && s.QuantityOnHand == 42);
    }

    private async Task<Guid> PlaceTestOrderAsync(IDispatcher dispatcher)
    {
        var anonymousId = Guid.NewGuid();
        var cart = (await dispatcher.Send(new GetOrCreateCartCommand(null, anonymousId))).Value;

        // This test doesn't need a real Catalog/Inventory-backed line item (it only exercises
        // Order status transitions, not checkout re-validation) — but PlaceOrderCommand always
        // re-validates against Catalog/Inventory (ADR-014), so route through AddCartItemCommand
        // against the already-seeded stock item's variant, same as PaymentWebhookTests, rather
        // than constructing an Order directly and bypassing the real write path.
        await dispatcher.Send(new AddCartItemCommand(cart.Id, _stockVariantId, 1));

        var address = new AddressInput("Admin Test", "+201000000002", "1 Test St", null, "Cairo", null, "11511", "EG");
        var placeResult = await dispatcher.Send(new PlaceOrderCommand(cart.Id, null, address, address, 10m, "admin-ops-test"));
        return placeResult.Value;
    }
}
