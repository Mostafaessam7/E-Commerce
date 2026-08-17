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
using Promotions.Application.Coupons;
using Promotions.Domain;
using Promotions.Infrastructure;
using Promotions.Infrastructure.Persistence;
using Security;

namespace IntegrationTests.Promotions;

/// <summary>
/// Proves the coupon redemption Ordering's checkout dispatches into Promotions (ADR-014) actually
/// applies a real discount and increments real usage — and that a coupon redeemed for an order
/// that then fails to place (insufficient stock) gets its usage released, not silently burned.
/// </summary>
public sealed class CouponCheckoutTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";
    private const string TestNotesMarker = "integration-test-coupon-checkout";

    private ServiceProvider _provider = null!;
    private Guid _productId;
    private Guid _variantId;
    private Guid _stockItemId;
    private Guid _couponId;
    private string _couponCode = null!;

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
        services.AddPromotionsModule(configuration);

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var product = Product.Create($"Coupon Test Product {Guid.NewGuid():N}", $"coupon-test-{Guid.NewGuid():N}", null, null, brandId: null).Value;
        var variantResult = product.AddVariant($"CPN-{Guid.NewGuid():N}"[..20], 200m, "EGP", salePrice: null, barcode: null, weightKg: null);
        product.Publish();
        catalogDb.Products.Add(product);
        await catalogDb.SaveChangesAsync();
        _productId = product.Id;
        _variantId = variantResult.Value;

        var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var stockItem = StockItem.Create(_variantId, initialQuantity: 1).Value;
        inventoryDb.StockItems.Add(stockItem);
        await inventoryDb.SaveChangesAsync();
        _stockItemId = stockItem.Id;

        _couponCode = $"TEST{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var createResult = await dispatcher.Send(new CreateCouponCommand(_couponCode, DiscountType.Percentage, 10m, "EGP", null, null, null));
        _couponId = createResult.Value;
    }

    public async Task DisposeAsync()
    {
        using var scope = _provider.CreateScope();

        var promotionsDb = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        await promotionsDb.Coupons.Where(c => c.Id == _couponId).ExecuteDeleteAsync();

        var orderingDb = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await orderingDb.Orders.Where(o => o.Notes == TestNotesMarker).ExecuteDeleteAsync();
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
    public async Task Placing_an_order_with_a_valid_coupon_applies_the_discount_and_burns_one_use()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var cart = (await dispatcher.Send(new GetOrCreateCartCommand(null, Guid.NewGuid()))).Value;
        await dispatcher.Send(new AddCartItemCommand(cart.Id, _variantId, 1));
        await dispatcher.Send(new ApplyCouponCommand(cart.Id, _couponCode));

        var address = new AddressInput("Coupon Test", "+201000000010", "1 Test St", null, "Cairo", null, "11511", "EG");
        var placeResult = await dispatcher.Send(
            new PlaceOrderCommand(cart.Id, null, "coupon-buyer@example.com", address, address, 10m, TestNotesMarker));

        placeResult.IsSuccess.Should().BeTrue();

        var order = (await dispatcher.Send(new GetOrderQuery(placeResult.Value))).Value;
        order.Discount.Should().Be(20m, "10% of the 200 EGP line total");
        order.Total.Should().Be(200m - 20m + 10m + Math.Round(200m * 0.14m, 2), "subtotal - discount + shipping + tax");

        var promotionsDb = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        var coupon = await promotionsDb.Coupons.AsNoTracking().SingleAsync(c => c.Id == _couponId);
        coupon.UsageCount.Should().Be(1);
    }

    [Fact]
    public async Task A_coupon_redeemed_for_an_order_that_fails_to_place_is_released_not_burned()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // Only 1 unit of stock exists (InitializeAsync) — asking for 2 makes stock reservation
        // fail *after* the coupon has already been redeemed, exercising the compensation path.
        var cart = (await dispatcher.Send(new GetOrCreateCartCommand(null, Guid.NewGuid()))).Value;
        await dispatcher.Send(new AddCartItemCommand(cart.Id, _variantId, 2));
        await dispatcher.Send(new ApplyCouponCommand(cart.Id, _couponCode));

        var address = new AddressInput("Coupon Test", "+201000000011", "1 Test St", null, "Cairo", null, "11511", "EG");
        var placeResult = await dispatcher.Send(
            new PlaceOrderCommand(cart.Id, null, "coupon-buyer2@example.com", address, address, 10m, TestNotesMarker));

        placeResult.IsFailure.Should().BeTrue();

        var promotionsDb = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        var coupon = await promotionsDb.Coupons.AsNoTracking().SingleAsync(c => c.Id == _couponId);
        coupon.UsageCount.Should().Be(0, "the failed order must not have permanently consumed the coupon's usage limit");
    }
}
