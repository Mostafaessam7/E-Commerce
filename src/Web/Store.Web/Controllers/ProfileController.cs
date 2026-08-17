using Customers.Application.Profile;
using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public ProfileController(IDispatcher dispatcher, ICurrentUser currentUser)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(cancellationToken);
        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileFormModel form, CancellationToken cancellationToken)
    {
        var customerId = _currentUser.UserId!.Value;
        var result = await _dispatcher.Send(new UpdateProfileCommand(customerId, form.FullName, form.Phone), cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Profile updated." : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAddress(AddAddressFormModel form, CancellationToken cancellationToken)
    {
        var customerId = _currentUser.UserId!.Value;
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please fill in all required address fields.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _dispatcher.Send(
            new AddAddressCommand(
                customerId, form.Label, form.FullName, form.Phone, form.Line1, form.Line2,
                form.City, form.State, form.PostalCode, form.Country, form.IsDefault),
            cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Address added." : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAddress(Guid addressId, CancellationToken cancellationToken)
    {
        var customerId = _currentUser.UserId!.Value;
        var result = await _dispatcher.Send(new RemoveAddressCommand(customerId, addressId), cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Address removed." : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultAddress(Guid addressId, CancellationToken cancellationToken)
    {
        var customerId = _currentUser.UserId!.Value;
        var result = await _dispatcher.Send(new SetDefaultAddressCommand(customerId, addressId), cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Default address updated." : result.Error.Message;
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
