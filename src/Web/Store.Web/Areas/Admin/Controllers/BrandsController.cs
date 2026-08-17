using Catalog.Application.Brands;
using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security;
using Store.Web.Areas.Admin.Models;

namespace Store.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Catalog.View)]
public sealed class BrandsController : Controller
{
    private readonly IDispatcher _dispatcher;

    public BrandsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ListBrandsQuery(IncludeInactive: true), cancellationToken);
        return View(result.Value);
    }

    [Authorize(Policy = Permissions.Catalog.Create)]
    public IActionResult Create() => View(new CreateBrandFormModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Create)]
    public async Task<IActionResult> Create(CreateBrandFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await _dispatcher.Send(new CreateBrandCommand(form.Name, form.Slug), cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            return View(form);
        }

        TempData["Success"] = "Brand created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeactivateBrandCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Brand deactivated." : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateBrandCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Brand activated." : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }
}
