using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public CouponsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

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

        TempData["Success"] = "Coupon created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Promotions.Manage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeactivateCouponCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Coupon deactivated." : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Promotions.Manage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateCouponCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Coupon activated." : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }
}
