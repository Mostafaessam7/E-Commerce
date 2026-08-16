using Catalog.Application.Products;
using Inventory.Application.Stock;
using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ordering.Application.Checkout;
using Security;
using Store.Web.Areas.Admin.Models;

namespace Store.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Reports.View)]
public sealed class DashboardController : Controller
{
    private readonly IDispatcher _dispatcher;

    public DashboardController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Counts only, no per-row data — one PageSize=1 search per module reuses the existing
        // paged queries' TotalCount rather than adding a dedicated "count" query per module.
        var productsResult = await _dispatcher.Send(
            new SearchProductsQuery(new ProductSearchCriteria(Page: 1, PageSize: 1, IncludeAllStatuses: true)), cancellationToken);
        var pendingOrdersResult = await _dispatcher.Send(
            new SearchOrdersQuery(new OrderSearchCriteria(Status: "Pending", Page: 1, PageSize: 1)), cancellationToken);
        var allOrdersResult = await _dispatcher.Send(
            new SearchOrdersQuery(new OrderSearchCriteria(Page: 1, PageSize: 1)), cancellationToken);
        var stockResult = await _dispatcher.Send(new SearchStockQuery(Page: 1, PageSize: 1), cancellationToken);

        var model = new DashboardViewModel(
            TotalProducts: productsResult.Value.TotalCount,
            TotalOrders: allOrdersResult.Value.TotalCount,
            PendingOrders: pendingOrdersResult.Value.TotalCount,
            TrackedStockItems: stockResult.Value.TotalCount);

        return View(model);
    }
}
