using System.Diagnostics;
using Catalog.Application.Brands;
using Catalog.Application.Categories;
using Catalog.Application.Products;
using Messaging;
using Microsoft.AspNetCore.Mvc;
using Shipping.Contracts;
using Store.Web.Models;

namespace Store.Web.Controllers;

public class HomeController : Controller
{
    private readonly IDispatcher _dispatcher;

    public HomeController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Sequential, not Task.WhenAll: the dispatcher's handlers share this request's scoped
        // DbContext, which EF Core does not allow concurrent operations against — same reasoning
        // as every other multi-query controller action in this codebase (e.g. ProductsController's
        // PopulateBrandsAndCategoriesAsync).
        var featuredCriteria = new ProductSearchCriteria(FeaturedOnly: true, PageSize: 8, SortBy: ProductSortOrder.Newest);
        var featured = await _dispatcher.Send(new SearchProductsQuery(featuredCriteria), cancellationToken);

        var newArrivalsCriteria = new ProductSearchCriteria(PageSize: 4, SortBy: ProductSortOrder.Newest);
        var newArrivals = await _dispatcher.Send(new SearchProductsQuery(newArrivalsCriteria), cancellationToken);

        var categories = await _dispatcher.Send(new ListCategoriesQuery(), cancellationToken);
        var brands = await _dispatcher.Send(new ListBrandsQuery(), cancellationToken);

        var model = new HomeViewModel(
            featured.IsSuccess ? featured.Value.Items : [],
            newArrivals.IsSuccess ? newArrivals.Value.Items : [],
            categories.IsSuccess ? categories.Value : [],
            brands.IsSuccess ? brands.Value : []);

        return View(model);
    }

    public IActionResult Privacy() => View();

    public IActionResult About() => View();

    public IActionResult Contact() => View();

    public IActionResult Faq() => View();

    public IActionResult Returns() => View();

    public IActionResult Terms() => View();

    public async Task<IActionResult> Shipping(CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ListShippingMethodsQuery(), cancellationToken);
        return View(result.IsSuccess ? result.Value : []);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
