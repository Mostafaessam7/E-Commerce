using Customers.Application.Profile;
using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Ordering.Application.Checkout;
using Security;
using Store.Web.Models;

namespace Store.Web.Controllers;

/// <summary>
/// "My Account" — profile + saved address book for any signed-in customer (no special
/// permission beyond being authenticated, unlike the Admin area). <see cref="Customer.Id"/>
/// (Customers.Domain) is always the same Guid as <see cref="ICurrentUser.UserId"/> — this
/// controller is the one place that equality is assumed (docs/modules.md).
/// </summary>
[Authorize]
public sealed class ProfileController : Controller
{
    private readonly IDispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ProfileController(IDispatcher dispatcher, ICurrentUser currentUser, IStringLocalizer<SharedResource> localizer)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(cancellationToken);
        return View(profile);
    }

    // Phase 37 (ADR-048): the only prior way to see an order was the one-time Confirmation page
    // right after placing it — Order.CustomerId has been set since Phase 28, but nothing ever
    // queried by it. CustomerId is always the signed-in user's own id (never request-supplied),
    // same invariant as every other action on this controller — a customer can only ever see
    // their own orders.
    public async Task<IActionResult> Orders(int page = 1, CancellationToken cancellationToken = default)
    {
        var customerId = _currentUser.UserId!.Value;
        var criteria = new OrderSearchCriteria(CustomerId: customerId, Page: page < 1 ? 1 : page, PageSize: 10);
        var result = await _dispatcher.Send(new SearchOrdersQuery(criteria), cancellationToken);

        return View(result.IsSuccess ? result.Value : new OrderSearchResultDto([], 0, criteria.Page, criteria.PageSize));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileFormModel form, CancellationToken cancellationToken)
    {
        var customerId = _currentUser.UserId!.Value;
        var result = await _dispatcher.Send(new UpdateProfileCommand(customerId, form.FullName, form.Phone), cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _localizer["Profile updated."].Value : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAddress(AddAddressFormModel form, CancellationToken cancellationToken)
    {
        var customerId = _currentUser.UserId!.Value;
        if (!ModelState.IsValid)
        {
            TempData["Error"] = _localizer["Please fill in all required address fields."].Value;
            return RedirectToAction(nameof(Index));
        }

        var result = await _dispatcher.Send(
            new AddAddressCommand(
                customerId, form.Label, form.FullName, form.Phone, form.Line1, form.Line2,
                form.City, form.State, form.PostalCode, form.Country, form.IsDefault),
            cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _localizer["Address added."].Value : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAddress(Guid addressId, CancellationToken cancellationToken)
    {
        var customerId = _currentUser.UserId!.Value;
        var result = await _dispatcher.Send(new RemoveAddressCommand(customerId, addressId), cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _localizer["Address removed."].Value : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultAddress(Guid addressId, CancellationToken cancellationToken)
    {
        var customerId = _currentUser.UserId!.Value;
        var result = await _dispatcher.Send(new SetDefaultAddressCommand(customerId, addressId), cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _localizer["Default address updated."].Value : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<CustomerProfileDto> GetOrCreateProfileAsync(CancellationToken cancellationToken)
    {
        var customerId = _currentUser.UserId!.Value;
        var email = _currentUser.Email ?? string.Empty;
        var result = await _dispatcher.Send(new GetOrCreateCustomerCommand(customerId, email), cancellationToken);
        return result.Value;
    }
}
