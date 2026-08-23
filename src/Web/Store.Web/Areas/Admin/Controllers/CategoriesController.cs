using Catalog.Application.Categories;
using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Security;
using Store.Web.Areas.Admin.Models;

namespace Store.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Catalog.View)]
public sealed class CategoriesController : Controller
{
    private readonly IDispatcher _dispatcher;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CategoriesController(IDispatcher dispatcher, IStringLocalizer<SharedResource> localizer)
    {
        _dispatcher = dispatcher;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ListCategoriesQuery(IncludeInactive: true), cancellationToken);
        return View(result.Value);
    }

    [Authorize(Policy = Permissions.Catalog.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var categories = await _dispatcher.Send(new ListCategoriesQuery(IncludeInactive: true), cancellationToken);
        ViewBag.Categories = categories.Value;
        return View(new CreateCategoryFormModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Create)]
    public async Task<IActionResult> Create(CreateCategoryFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var categories = await _dispatcher.Send(new ListCategoriesQuery(IncludeInactive: true), cancellationToken);
            ViewBag.Categories = categories.Value;
            return View(form);
        }

        var result = await _dispatcher.Send(new CreateCategoryCommand(form.Name, form.Slug, form.ParentId), cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            var categories = await _dispatcher.Send(new ListCategoriesQuery(IncludeInactive: true), cancellationToken);
            ViewBag.Categories = categories.Value;
            return View(form);
        }

        TempData["Success"] = _localizer["Category created."].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeactivateCategoryCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _localizer["Category deactivated."].Value : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateCategoryCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _localizer["Category activated."].Value : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }
}
