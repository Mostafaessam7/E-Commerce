using Catalog.Application.Products;
using Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Store.Web.Controllers;

public class ShopController : Controller
{
    private readonly IDispatcher _dispatcher;

    public ShopController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<IActionResult> Index(
        string? q,
        Guid? categoryId,
        Guid? brandId,
        decimal? minPrice,
        decimal? maxPrice,
        ProductSortOrder sort = ProductSortOrder.Newest,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var criteria = new ProductSearchCriteria(
            SearchTerm: q,
            CategoryId: categoryId,
            BrandId: brandId,
            MinPrice: minPrice,
            MaxPrice: maxPrice,
            Page: page < 1 ? 1 : page,
            PageSize: 12,
            SortBy: sort);

        var result = await _dispatcher.Send(new SearchProductsQuery(criteria), cancellationToken);

        return View(result.IsSuccess
            ? result.Value
            : new ProductSearchResultDto([], 0, criteria.Page, criteria.PageSize));
    }
}
