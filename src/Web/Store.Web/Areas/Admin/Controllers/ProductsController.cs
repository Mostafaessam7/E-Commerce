using Catalog.Application.Brands;
using Catalog.Application.Categories;
using Catalog.Application.Products;
using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security;
using Store.Web.Areas.Admin.Models;
using Store.Web.Infrastructure.Uploads;

namespace Store.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Catalog.View)]
public sealed class ProductsController : Controller
{
    private readonly IDispatcher _dispatcher;
    private readonly IProductImageStorage _imageStorage;

    public ProductsController(IDispatcher dispatcher, IProductImageStorage imageStorage)
    {
        _dispatcher = dispatcher;
        _imageStorage = imageStorage;
    }

    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(
            new SearchProductsQuery(new ProductSearchCriteria(Page: page, PageSize: 20, IncludeAllStatuses: true)), cancellationToken);

        return View(result.Value);
    }

    [Authorize(Policy = Permissions.Catalog.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateBrandsAndCategoriesAsync(cancellationToken);
        return View(new ProductFormModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Create)]
    public async Task<IActionResult> Create(ProductFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateBrandsAndCategoriesAsync(cancellationToken);
            return View(form);
        }

        var result = await _dispatcher.Send(
            new CreateProductCommand(form.Name, form.Slug, form.ShortDescription, form.Description, form.BrandId, form.CategoryIds),
            cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            await PopulateBrandsAndCategoriesAsync(cancellationToken);
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

        await PopulateBrandsAndCategoriesAsync(cancellationToken);
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
            await PopulateBrandsAndCategoriesAsync(cancellationToken);
            return View(reload.Value);
        }

        var result = await _dispatcher.Send(
            new UpdateProductCommand(form.Id, form.Name, form.ShortDescription, form.Description, form.BrandId, form.CategoryIds),
            cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            var reload = await _dispatcher.Send(new GetProductByIdQuery(form.Id), cancellationToken);
            await PopulateBrandsAndCategoriesAsync(cancellationToken);
            return View(reload.Value);
        }

        TempData["Success"] = "Product updated.";
        return RedirectToAction(nameof(Edit), new { id = form.Id });
    }

    private async Task PopulateBrandsAndCategoriesAsync(CancellationToken cancellationToken)
    {
        var brands = await _dispatcher.Send(new ListBrandsQuery(), cancellationToken);
        var categories = await _dispatcher.Send(new ListCategoriesQuery(), cancellationToken);
        ViewBag.Brands = brands.Value;
        ViewBag.Categories = categories.Value;
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
    public async Task<IActionResult> Feature(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new FeatureProductCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Product marked as featured." : result.Error.Message;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> Unfeature(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UnfeatureProductCommand(id), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Product removed from featured." : result.Error.Message;
        return RedirectToAction(nameof(Edit), new { id });
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
    [RequestSizeLimit(5_242_880)] // 5 MB — matches LocalProductImageStorage.MaxFileSizeBytes
    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> UploadImage(Guid productId, IFormFile? file, bool isPrimary, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            TempData["Error"] = "Choose an image file first.";
            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        var saveResult = await _imageStorage.SaveAsync(productId, file, cancellationToken);
        if (saveResult.IsFailure)
        {
            TempData["Error"] = saveResult.Error.Message;
            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        var result = await _dispatcher.Send(
            new AddProductImageCommand(productId, saveResult.Value, file.FileName, isPrimary), cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Image uploaded." : result.Error.Message;
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    public async Task<IActionResult> RemoveImage(Guid productId, Guid imageId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveProductImageCommand(productId, imageId), cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Image removed." : result.Error.Message;
        return RedirectToAction(nameof(Edit), new { id = productId });
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
