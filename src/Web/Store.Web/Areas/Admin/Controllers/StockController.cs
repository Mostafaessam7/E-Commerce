using Inventory.Application.Stock;
using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Security;
using Store.Web.Areas.Admin.Models;

namespace Store.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Inventory.View)]
public sealed class StockController : Controller
{
    private readonly IDispatcher _dispatcher;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public StockController(IDispatcher dispatcher, IStringLocalizer<SharedResource> localizer)
    {
        _dispatcher = dispatcher;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new SearchStockQuery(page, PageSize: 20), cancellationToken);
        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Inventory.Adjust)]
    public async Task<IActionResult> Adjust(AdjustStockFormModel form, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new AdjustStockCommand(form.ProductVariantId, form.NewQuantityOnHand, form.Reason), cancellationToken);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? _localizer["Stock adjusted."].Value : result.Error.Message;
        return RedirectToAction(nameof(Index));
    }
}
