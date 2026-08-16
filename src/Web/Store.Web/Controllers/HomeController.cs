using System.Diagnostics;
using Catalog.Application.Products;
using Messaging;
using Microsoft.AspNetCore.Mvc;
using Store.Web.Models;

namespace Store.Web.Controllers;

public class HomeController : Controller
{
    private readonly IDispatcher _dispatcher;

    public HomeController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var criteria = new ProductSearchCriteria(FeaturedOnly: true, PageSize: 8, SortBy: ProductSortOrder.Newest);
        var result = await _dispatcher.Send(new SearchProductsQuery(criteria), cancellationToken);

        return View(result.IsSuccess ? result.Value.Items : []);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
