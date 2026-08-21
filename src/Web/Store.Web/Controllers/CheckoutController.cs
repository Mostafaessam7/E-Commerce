using Customers.Application.Profile;
using Messaging;
using Microsoft.AspNetCore.Mvc;
using Ordering.Application.Checkout;
using Security;
using Shipping.Contracts;
using Store.Web.Infrastructure;
using Store.Web.Infrastructure.ExceptionHandling;
using Store.Web.Models;

namespace Store.Web.Controllers;

public class CheckoutController : Controller
{
    private readonly IDispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;

    public CheckoutController(IDispatcher dispatcher, ICurrentUser currentUser)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var form = new CheckoutFormModel();
        await PopulateShippingMethodsAsync(form, cancellationToken);
        await PrefillFromCustomerProfileAsync(form, cancellationToken);
        return View(form);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(CheckoutFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateShippingMethodsAsync(form, cancellationToken);
            return View(nameof(Index), form);
        }

        var anonymousId = HttpContext.GetOrSetAnonymousId();
        var customerId = _currentUser.IsAuthenticated ? _currentUser.UserId : null;
        var cartResult = await _dispatcher.Send(
            new Ordering.Application.Carts.GetOrCreateCartCommand(CustomerId: customerId, AnonymousId: anonymousId), cancellationToken);

        var shippingAddress = new AddressInput(form.FullName, form.Phone, form.Line1, form.Line2, form.City, form.State, form.PostalCode, form.Country);
        var billingAddress = form.BillingSameAsShipping
            ? shippingAddress
            : new AddressInput(form.BillingFullName!, form.BillingPhone!, form.BillingLine1!, form.BillingLine2, form.BillingCity!, form.BillingState, form.BillingPostalCode!, form.BillingCountry!);

        var placeResult = await _dispatcher.Send(
            new PlaceOrderCommand(cartResult.Value.Id, customerId, form.Email, billingAddress, shippingAddress, form.ShippingMethodId, form.Notes),
            cancellationToken);

        if (placeResult.IsFailure)
        {
            ModelState.AddModelError(string.Empty, placeResult.Error.Message);
            await PopulateShippingMethodsAsync(form, cancellationToken);
            return View(nameof(Index), form);
        }

        return RedirectToAction(nameof(Confirmation), new { orderId = placeResult.Value });
    }

    // Phase 37 (ADR-048): a real ownership check, not just "resolves the order" - a customer
    // order (CustomerId set, since Phase 28) can only be viewed by that same signed-in customer.
    // A guest order (CustomerId null) stays viewable by anyone holding the link, same as before -
    // there's no session-token linkage for guest orders to check against, and this is the exact
    // link Checkout/PlaceOrder itself redirects a fresh guest to immediately after placing one.
    public async Task<IActionResult> Confirmation(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetOrderQuery(orderId), cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var order = result.Value;
        if (order.CustomerId is Guid ownerId && ownerId != _currentUser.UserId)
        {
            return NotFound();
        }

        return View(order);
    }

    private async Task PopulateShippingMethodsAsync(CheckoutFormModel form, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ListShippingMethodsQuery(), cancellationToken);
        form.ShippingMethods = result.IsSuccess ? result.Value : [];
    }

    // Phase 28 (ADR-028's deferred follow-up): a signed-in customer with a saved default address
    // shouldn't have to retype it every order. Only pre-fills the *initial* GET — a validation
    // failure re-render keeps whatever the shopper already typed (PlaceOrder never calls this),
    // and nothing here is trusted for the actual order — PlaceOrderCommand still takes the address
    // straight from the submitted form, same as a guest checkout.
    private async Task PrefillFromCustomerProfileAsync(CheckoutFormModel form, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid customerId)
        {
            return;
        }

        form.Email = _currentUser.Email ?? form.Email;

        var profileResult = await _dispatcher.Send(new GetCustomerProfileQuery(customerId), cancellationToken);
        if (profileResult.IsFailure)
        {
            return;
        }

        var profile = profileResult.Value;
        form.FullName = profile.FullName ?? form.FullName;
        form.Phone = profile.Phone ?? form.Phone;

        var defaultAddress = profile.Addresses.FirstOrDefault(a => a.IsDefault) ?? profile.Addresses.FirstOrDefault();
        if (defaultAddress is null)
        {
            return;
        }

        form.FullName = defaultAddress.FullName;
        form.Phone = defaultAddress.Phone;
        form.Line1 = defaultAddress.Line1;
        form.Line2 = defaultAddress.Line2;
        form.City = defaultAddress.City;
        form.State = defaultAddress.State;
        form.PostalCode = defaultAddress.PostalCode;
        form.Country = defaultAddress.Country;
    }
}
