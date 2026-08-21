using Catalog.Application.Brands;
using Catalog.Application.Categories;
using Catalog.Application.Products;

namespace Store.Web.Models;

/// <summary>
/// Phase 37 (ADR-048): the homepage used to render off a bare `IReadOnlyList&lt;ProductSummaryDto&gt;`
/// (Featured products only). Real category/brand/new-arrivals data already existed elsewhere in the
/// app (admin's Brand/Category management, Shop's own category/brand query-string filters) but was
/// never surfaced on the homepage — this composite model is what lets the view show all of it
/// without a parallel query per section handler.
/// </summary>
public sealed record HomeViewModel(
    IReadOnlyList<ProductSummaryDto> FeaturedProducts,
    IReadOnlyList<ProductSummaryDto> NewArrivals,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<BrandDto> Brands);
