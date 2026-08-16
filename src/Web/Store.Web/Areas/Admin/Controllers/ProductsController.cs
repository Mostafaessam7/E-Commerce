using Catalog.Application.Products;
using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security;
using Store.Web.Areas.Admin.Models;

namespace Store.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Catalog.View)]
public sealed class ProductsController : Controller
{
    private readonly IDispatcher _dispatcher;

    public ProductsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(
            new SearchProductsQuery(new ProductSearchCriteria(Page: page, PageSize: 20, IncludeAllStatuses: true)), cancellationToken);

        return View(result.Value);
    }

    [Authorize(Policy = Permissions.Catalog.Create)]
    public IActionResult Create() => View(new ProductFormModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Create)]
    public async Task<IActionResult> Create(ProductFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await _dispatcher.Send(
            new CreateProductCommand(form.Name, form.Slug, form.ShortDescription, form.Description, BrandId: null, CategoryIds: null),
            cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            return View(form);
        }

        TempData["Success"] = "Product created as a draft. Add a variant, then publish it.";
        return RedirectToAction(nameof(Edit), new { id = result.Value });
    }

    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetProductByIdQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> Edit(ProductEditFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var reload = await _dispatcher.Send(new GetProductByIdQuery(form.Id), cancellationToken);
            return View(reload.Value);
        }

        var result = await _dispatcher.Send(
            new UpdateProductCommand(form.Id, form.Name, form.ShortDescription, form.Description), cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            var reload = await _dispatcher.Send(new GetProductByIdQuery(form.Id), cancellationToken);
            return View(reload.Value);
        }

        TempData["Success"] = "Product updated.";
        return RedirectToAction(nameof(Edit), new { id = form.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> AddVariant(AddVariantFormModel form, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new AddProductVariantCommand(form.ProductId, form.Sku, form.Price, form.Currency, form.SalePrice), cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Variant added." : result.Error.Message;
        return RedirectToAction(nameof(Edit), new { id = form.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new PublishProductCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Product published." : result.Error.Message;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveProductCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Product archived." : result.Error.Message;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteProductCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Product deleted." : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }
}
