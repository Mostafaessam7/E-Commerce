using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Promotions.Application.Coupons;
using Promotions.Domain;
using Security;
using Store.Web.Areas.Admin.Models;

namespace Store.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Promotions.View)]
public sealed class CouponsController : Controller
{
    private readonly IDispatcher _dispatcher;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CouponsController(IDispatcher dispatcher, IStringLocalizer<SharedResource> localizer)
    {
        _dispatcher = dispatcher;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ListCouponsQuery(), cancellationToken);
        return View(result.Value);
    }

    [Authorize(Policy = Permissions.Promotions.Manage)]
    public IActionResult Create() => View(new CreateCouponFormModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Promotions.Manage)]
    public async Task<IActionResult> Create(CreateCouponFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await _dispatcher.Send(
            new CreateCouponCommand(
                form.Code, form.DiscountType, form.Value, form.Currency,
                form.ExpiresAtUtc, form.UsageLimit, form.MinimumOrderAmount),
            cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            return View(form);
        }

        TempData["Success"] = _localizer["Coupon created."].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Promotions.Manage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeactivateCouponCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _localizer["Coupon deactivated."].Value : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Promotions.Manage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateCouponCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _localizer["Coupon activated."].Value : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }
}
