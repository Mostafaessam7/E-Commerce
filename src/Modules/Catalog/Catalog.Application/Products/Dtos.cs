namespace Catalog.Application.Products;

public sealed record ProductVariantDto(Guid Id, string Sku, decimal Price, string Currency, decimal? SalePrice);

public sealed record ProductImageDto(Guid Id, string Url, string? AltText, bool IsPrimary);

public sealed record ProductDetailsDto(
    Guid Id,
    string Name,
    string Slug,
    string? ShortDescription,
    string? Description,
    Guid? BrandId,
    string Status,
    bool IsFeatured,
    string? MetaTitle,
    string? MetaDescription,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductImageDto> Images);

/// <summary>Listing-page projection — only what a product card needs, never the full aggregate.</summary>
public sealed record ProductSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    decimal? MinPrice,
    decimal? MinSalePrice,
    string? Currency,
    string? PrimaryImageUrl);

public sealed record ProductSearchCriteria(
    string? SearchTerm = null,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool? FeaturedOnly = null,
    int Page = 1,
    int PageSize = 20,
    ProductSortOrder SortBy = ProductSortOrder.Newest);

public enum ProductSortOrder
{
    Newest,
    PriceLowToHigh,
    PriceHighToLow,
    NameAToZ,
}

public sealed record ProductSearchResultDto(IReadOnlyList<ProductSummaryDto> Items, int TotalCount, int Page, int PageSize);
