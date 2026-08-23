using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Security;
using Shipping.Application.Methods;
using Shipping.Contracts;
using Store.Web.Areas.Admin.Models;

namespace Store.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Shipping.View)]
public sealed class ShippingMethodsController : Controller
{
    private readonly IDispatcher _dispatcher;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ShippingMethodsController(IDispatcher dispatcher, IStringLocalizer<SharedResource> localizer)
    {
        _dispatcher = dispatcher;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ListShippingMethodsQuery(IncludeInactive: true), cancellationToken);
        return View(result.Value);
    }

    [Authorize(Policy = Permissions.Shipping.Manage)]
    public IActionResult Create() => View(new CreateShippingMethodFormModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Shipping.Manage)]
    public async Task<IActionResult> Create(CreateShippingMethodFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await _dispatcher.Send(
            new CreateShippingMethodCommand(
                form.Name, form.Description, form.Cost, form.Currency, form.EstimatedDaysMin, form.EstimatedDaysMax),
            cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            return View(form);
        }

        TempData["Success"] = _localizer["Shipping method created."].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Shipping.Manage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeactivateShippingMethodCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _localizer["Shipping method deactivated."].Value : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Shipping.Manage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateShippingMethodCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _localizer["Shipping method activated."].Value : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }
}
