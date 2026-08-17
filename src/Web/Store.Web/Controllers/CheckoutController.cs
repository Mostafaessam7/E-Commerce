using Messaging;
using Microsoft.AspNetCore.Mvc;
using Ordering.Application.Checkout;
using Shipping.Contracts;
using Store.Web.Infrastructure;
using Store.Web.Infrastructure.ExceptionHandling;
using Store.Web.Models;

namespace Store.Web.Controllers;

public class CheckoutController : Controller
{
    private readonly IDispatcher _dispatcher;

    public CheckoutController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var form = new CheckoutFormModel();
        await PopulateShippingMethodsAsync(form, cancellationToken);
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
        var cartResult = await _dispatcher.Send(new Ordering.Application.Carts.GetOrCreateCartCommand(CustomerId: null, AnonymousId: anonymousId), cancellationToken);

        var shippingAddress = new AddressInput(form.FullName, form.Phone, form.Line1, form.Line2, form.City, form.State, form.PostalCode, form.Country);
        var billingAddress = form.BillingSameAsShipping
            ? shippingAddress
            : new AddressInput(form.BillingFullName!, form.BillingPhone!, form.BillingLine1!, form.BillingLine2, form.BillingCity!, form.BillingState, form.BillingPostalCode!, form.BillingCountry!);

        var placeResult = await _dispatcher.Send(
            new PlaceOrderCommand(cartResult.Value.Id, CustomerId: null, form.Email, billingAddress, shippingAddress, form.ShippingMethodId, form.Notes),
            cancellationToken);

        if (placeResult.IsFailure)
        {
            ModelState.AddModelError(string.Empty, placeResult.Error.Message);
            await PopulateShippingMethodsAsync(form, cancellationToken);
            return View(nameof(Index), form);
        }

        return RedirectToAction(nameof(Confirmation), new { orderId = placeResult.Value });
    }

    public async Task<IActionResult> Confirmation(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetOrderQuery(orderId), cancellationToken);
        return result.IsFailure ? NotFound() : View(result.Value);
    }

    private async Task PopulateShippingMethodsAsync(CheckoutFormModel form, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ListShippingMethodsQuery(), cancellationToken);
        form.ShippingMethods = result.IsSuccess ? result.Value : [];
    }
}
