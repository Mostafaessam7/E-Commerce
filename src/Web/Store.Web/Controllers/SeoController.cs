using System.Xml.Linq;
using Catalog.Application.Products;
using Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Store.Web.Controllers;

/// <summary>
/// Search-engine discoverability — `sitemap.xml` (every active product plus the storefront's
/// static pages) and `robots.txt` (points crawlers at the sitemap, keeps them out of the
/// account/cart/checkout/admin pages nobody wants indexed). Generated on request, not a static
/// file under `wwwroot/` — the product list changes as products are published/archived, and a
/// stale sitemap is worse than no sitemap.
/// </summary>
public sealed class SeoController : Controller
{
    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private readonly IDispatcher _dispatcher;

    public SeoController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var urlset = new XElement(SitemapNs + "urlset");

        urlset.Add(Url("", "daily", "1.0"));
        urlset.Add(Url("Shop", "daily", "0.9"));

        // A single large page rather than a paginated sitemap index — this catalog's real scale
        // doesn't come close to the ~50,000-URL point where a sitemap index actually earns its
        // complexity (see Google's sitemap size limits); revisit if that ever changes.
        var criteria = new ProductSearchCriteria(PageSize: 5000, SortBy: ProductSortOrder.Newest);
        var result = await _dispatcher.Send(new SearchProductsQuery(criteria), cancellationToken);

        if (result.IsSuccess)
        {
            foreach (var product in result.Value.Items)
            {
                urlset.Add(Url($"product/{product.Slug}", "weekly", "0.8"));
            }
        }

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);
        return Content(document.ToString(), "application/xml");

        XElement Url(string path, string changeFreq, string priority) => new(
            SitemapNs + "url",
            new XElement(SitemapNs + "loc", string.IsNullOrEmpty(path) ? baseUrl : $"{baseUrl}/{path}"),
            new XElement(SitemapNs + "changefreq", changeFreq),
            new XElement(SitemapNs + "priority", priority));
    }

    [HttpGet("robots.txt")]
    public IActionResult Robots()
    {
        var sitemapUrl = $"{Request.Scheme}://{Request.Host}/sitemap.xml";

        var body = string.Join(
            '\n',
            "User-agent: *",
            "Allow: /",
            "Disallow: /Admin/",
            "Disallow: /Account/",
            "Disallow: /Cart",
            "Disallow: /Checkout",
            "Disallow: /Profile",
            $"Sitemap: {sitemapUrl}");

        return Content(body, "text/plain");
    }
}
