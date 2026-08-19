using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Customers.Infrastructure.Persistence;
using FluentAssertions;
using Identity.Infrastructure.Persistence;
using Inventory.Domain;
using Inventory.Infrastructure.Persistence;
using Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Infrastructure.Persistence;
using Ordering.Infrastructure.Persistence;
using Payments.Application.Abstractions;
using Payments.Application.Payments;
using Payments.Infrastructure.Persistence;
using Shipping.Application.Methods;
using Shipping.Infrastructure.Persistence;

namespace EndToEndTests;

/// <summary>
/// Drives the full guest-turned-registered-customer journey through the real HTTP pipeline —
/// register → confirm email (real link pulled from the real <c>NotificationLog</c> row, not
/// short-circuited) → log in → add to cart → checkout → pay (real webhook endpoint, real HMAC
/// signature verification) → order shows Paid. Every request goes through real MVC model binding,
/// real antiforgery validation, and real cookie-based auth/cart-identity — this is what actually
/// proves a browser-driven journey works, not just that the handlers behind it do (that's what
/// IntegrationTests already covers, module by module).
///
/// The one deliberate shortcut: payment initialization is dispatched directly via
/// <see cref="IDispatcher"/> from a DI scope rather than through <c>POST /Payments/Pay</c> — that
/// action makes its own outbound HTTP call back into the webhook endpoint via
/// <c>IHttpClientFactory</c>, which has no real socket to land on inside an in-memory
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> test host. The
/// webhook call itself is still made for real, through the real test <see cref="HttpClient"/>,
/// against the real <c>/api/webhooks/payments/fake</c> endpoint — the one thing that shortcut
/// would have exercised is a self-referencing network hop, not applicable logic.
/// </summary>
public sealed class RegisterCartCheckoutPaymentFlowTests : IClassFixture<StoreWebApplicationFactory>, IAsyncLifetime
{
    private static readonly Regex AntiForgeryTokenPattern = new(
        """name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)""",
        RegexOptions.Compiled);

    private static readonly Regex ConfirmationLinkPattern = new(
        @"https?://[^\s]+/Account/ConfirmEmail\?[^\s""<]+", RegexOptions.Compiled);

    private readonly StoreWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private Guid _productId;
    private Guid _variantId;
    private Guid _stockItemId;
    private Guid _shippingMethodId;
    private readonly string _email = $"e2e-{Guid.NewGuid():N}@example.com";
    private const string Password = "E2ePassword#123";

    public RegisterCartCheckoutPaymentFlowTests(StoreWebApplicationFactory factory)
    {
        _factory = factory;
        // BaseAddress must be https:// — AnonymousIdExtensions and the auth cookie are both
        // marked Secure, so a plain http:// client (the WebApplicationFactory default) would
        // silently never send them back on the next request, leaving every request looking like
        // a brand new anonymous visitor (the cart-goes-missing bug this comment is here to
        // prevent someone from reintroducing).
        _client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var product = Product.Create(
            $"E2E Test Product {Guid.NewGuid():N}", $"e2e-test-{Guid.NewGuid():N}", null, null, brandId: null).Value;
        var variantResult = product.AddVariant($"E2E-{Guid.NewGuid():N}"[..20], 150m, "EGP", salePrice: null, barcode: null, weightKg: null);
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

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var shippingResult = await dispatcher.Send(new CreateShippingMethodCommand("E2E Standard", null, 25m, "EGP", 3, 5));
        _shippingMethodId = shippingResult.Value;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var shippingDb = scope.ServiceProvider.GetRequiredService<ShippingDbContext>();
        await shippingDb.ShippingMethods.Where(m => m.Id == _shippingMethodId).ExecuteDeleteAsync();

        var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await inventoryDb.StockItems.Where(s => s.Id == _stockItemId).ExecuteDeleteAsync();

        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await catalogDb.Products.Where(p => p.Id == _productId).ExecuteDeleteAsync();

        var notificationsDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await notificationsDb.NotificationLogs.Where(n => n.Recipient == _email).ExecuteDeleteAsync();

        var paymentsDb = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        await paymentsDb.ProcessedWebhookEvents.ExecuteDeleteAsync();

        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var user = await identityDb.Users.FirstOrDefaultAsync(u => u.Email == _email);

        var orderingDb = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await orderingDb.Orders.Where(o => o.Email == _email).ExecuteDeleteAsync();
        await orderingDb.Carts.Where(c => c.AnonymousId != null || (user != null && c.CustomerId == user.Id)).ExecuteDeleteAsync();

        if (user is not null)
        {
            var customersDb = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
            await customersDb.Customers.Where(c => c.Id == user.Id).ExecuteDeleteAsync();

            identityDb.Users.Remove(user);
            await identityDb.SaveChangesAsync();
        }

        _client.Dispose();
    }

    [Fact]
    public async Task Guest_registers_confirms_email_logs_in_checks_out_and_pays_through_the_real_http_pipeline()
    {
        // --- Register ---
        var registerPageHtml = await GetString("/Account/Register");
        var registerToken = ExtractAntiForgeryToken(registerPageHtml);

        var registerResponse = await _client.PostAsync("/Account/Register", FormBody(new()
        {
            ["Email"] = _email,
            ["Password"] = Password,
            ["ConfirmPassword"] = Password,
            ["__RequestVerificationToken"] = registerToken,
        }));
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK, "a successful registration renders the RegisterConfirmation view directly");

        // --- Confirm email using the real link from the real NotificationLog row ---
        using (var scope = _factory.Services.CreateScope())
        {
            var notificationsDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var confirmationLog = await notificationsDb.NotificationLogs
                .OrderByDescending(n => n.SentAtUtc)
                .FirstOrDefaultAsync(n => n.Recipient == _email && n.Subject == "Confirm your email");

            confirmationLog.Should().NotBeNull("registering must actually enqueue a real confirmation email, not just create the account");

            var match = ConfirmationLinkPattern.Match(confirmationLog!.Body);
            match.Success.Should().BeTrue();

            var confirmUri = new Uri(match.Value);
            var confirmResponse = await _client.GetAsync(confirmUri.PathAndQuery);
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            (await confirmResponse.Content.ReadAsStringAsync()).Should().Contain("Email Confirmed", "the ConfirmEmailSuccess view must actually render");
        }

        // --- Log in ---
        var loginPageHtml = await GetString("/Account/Login");
        var loginToken = ExtractAntiForgeryToken(loginPageHtml);

        var loginResponse = await _client.PostAsync("/Account/Login", FormBody(new()
        {
            ["Email"] = _email,
            ["Password"] = Password,
            ["__RequestVerificationToken"] = loginToken,
        }));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect, "a successful login redirects away from the login page");

        // --- Add to cart ---
        var productSlugPath = await ResolveProductSlugAsync();
        var productPageHtml = await GetString(productSlugPath);
        var addToCartToken = ExtractAntiForgeryToken(productPageHtml);

        var addToCartResponse = await _client.PostAsync("/Cart/Add", FormBody(new()
        {
            ["productVariantId"] = _variantId.ToString(),
            ["quantity"] = "2",
            ["__RequestVerificationToken"] = addToCartToken,
        }));
        addToCartResponse.StatusCode.Should().Be(HttpStatusCode.Redirect, "adding to cart redirects to the cart page");

        // --- Checkout ---
        var checkoutPageHtml = await GetString("/Checkout");
        checkoutPageHtml.Should().Contain("E2E Standard", "the real seeded shipping method must appear in the picker, not a hardcoded one");
        var checkoutToken = ExtractAntiForgeryToken(checkoutPageHtml);

        var placeOrderResponse = await _client.PostAsync("/Checkout/PlaceOrder", FormBody(new()
        {
            ["Email"] = _email,
            ["FullName"] = "E2E Test Customer",
            ["Phone"] = "+201000000099",
            ["Line1"] = "1 End To End St",
            ["City"] = "Cairo",
            ["PostalCode"] = "11511",
            ["Country"] = "EG",
            ["BillingSameAsShipping"] = "true",
            ["ShippingMethodId"] = _shippingMethodId.ToString(),
            ["__RequestVerificationToken"] = checkoutToken,
        }));
        placeOrderResponse.StatusCode.Should().Be(HttpStatusCode.Redirect, "a successful checkout redirects to the order confirmation page");

        var confirmationLocation = placeOrderResponse.Headers.Location!.ToString();
        confirmationLocation.Should().Contain("/Checkout/Confirmation");
        var orderId = Guid.Parse(confirmationLocation.Split("orderId=")[1]);

        var confirmationHtml = await GetString(confirmationLocation);
        confirmationHtml.Should().Contain("Payment: Pending", "the order must start unpaid before the webhook fires");

        // --- Pay: initialize (dispatched directly — see class doc comment for why) + a real
        // webhook POST through the real endpoint, real signature, real HTTP round trip ---
        string payload;
        string signature;
        using (var scope = _factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            var orderResult = await dispatcher.Send(new Ordering.Application.Checkout.GetOrderQuery(orderId));
            var initResult = await dispatcher.Send(new InitializePaymentCommand(orderId, orderResult.Value.Total, orderResult.Value.Currency));
            initResult.IsSuccess.Should().BeTrue();

            var simulator = scope.ServiceProvider.GetRequiredService<IWebhookSimulator>();
            (payload, signature) = simulator.BuildSucceededPayload(initResult.Value.PaymentTransactionId);
        }

        using var webhookContent = new StringContent(payload, Encoding.UTF8, "application/json");
        using var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/payments/fake") { Content = webhookContent };
        webhookRequest.Headers.Add("X-Payment-Signature", signature);
        var webhookResponse = await _client.SendAsync(webhookRequest);
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK, "the real webhook endpoint must accept a correctly signed payload");

        // --- Confirm the order really flipped to Paid, through the real page again ---
        var paidConfirmationHtml = await GetString(confirmationLocation);
        paidConfirmationHtml.Should().Contain("Payment: Paid");
        paidConfirmationHtml.Should().NotContain("Payment: Pending");
    }

    private async Task<string> ResolveProductSlugAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var product = await catalogDb.Products.AsNoTracking().FirstAsync(p => p.Id == _productId);
        return $"/product/{product.Slug.Value}";
    }

    private async Task<string> GetString(string relativeUrl)
    {
        var response = await _client.GetAsync(relativeUrl);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = AntiForgeryTokenPattern.Match(html);
        match.Success.Should().BeTrue("every form on a real page must carry a real antiforgery token");
        return match.Groups[1].Value;
    }

    private static FormUrlEncodedContent FormBody(Dictionary<string, string> fields) => new(fields);
}
