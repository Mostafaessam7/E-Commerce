using Catalog.Application.Products;
using Catalog.Contracts;
using Catalog.Domain;
using Catalog.Domain.ValueObjects;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Repositories;

internal sealed class ProductQueries : IProductQueries
{
    private readonly CatalogDbContext _db;

    public ProductQueries(CatalogDbContext db) => _db = db;

    public async Task<ProductVariantSnapshotDto?> GetVariantSnapshotAsync(Guid productVariantId, CancellationToken cancellationToken = default) =>
        await _db.Products
            .AsNoTracking()
            .SelectMany(p => p.Variants, (p, v) => new { Product = p, Variant = v })
            .Where(x => x.Variant.Id == productVariantId)
            .Select(x => new ProductVariantSnapshotDto(
                x.Variant.Id,
                x.Product.Id,
                x.Product.Name,
                x.Variant.Sku,
                x.Variant.Price.Amount,
                x.Variant.SalePrice != null ? x.Variant.SalePrice.Amount : null,
                x.Variant.Price.Currency,
                x.Product.Status == ProductStatus.Active && x.Variant.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ProductDetailsDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var slugResult = Slug.Create(slug);
        if (slugResult.IsFailure)
        {
            return null;
        }

        var target = slugResult.Value;

        // No overloaded `==` on value objects (see ValueObject's doc comment) — this compiles to
        // Expression.Equal, which EF Core's converted-property translation handles correctly.
        //
        // Mapped to the DTO in C# after loading, not via a .Select() projection: projecting
        // straight into ProductDetailsDto's constructor — which itself nests two
        // collection .Select()s (Variants, Images) plus a conditional owned-type (SalePrice)
        // access — crashes EF Core's query shaper with a NullReferenceException at query
        // *compile* time (reproduces even against an empty table). Loading the aggregate via
        // Include and mapping afterwards uses EF's well-trodden entity-materialization path
        // instead of that fragile nested-projection shape.
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Slug == target && p.Status == ProductStatus.Active, cancellationToken);

        return product is null ? null : ToDto(product);
    }

    private static ProductDetailsDto ToDto(Product p) => new(
        p.Id,
        p.Name,
        p.Slug.Value,
        p.ShortDescription,
        p.Description,
        p.BrandId,
        p.Status.ToString(),
        p.IsFeatured,
        p.Seo.MetaTitle,
        p.Seo.MetaDescription,
        p.Tags.ToList(),
        p.Variants.Select(v => new ProductVariantDto(v.Id, v.Sku, v.Price.Amount, v.Price.Currency, v.SalePrice?.Amount)).ToList(),
        p.Images.OrderBy(i => i.DisplayOrder).Select(i => new ProductImageDto(i.Id, i.Url, i.AltText, i.IsPrimary)).ToList());

    public async Task<ProductSearchResultDto> SearchAsync(ProductSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = _db.Products.AsNoTracking();
        if (!criteria.IncludeAllStatuses)
        {
            query = query.Where(p => p.Status == ProductStatus.Active);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{criteria.SearchTerm}%"));
        }

        if (criteria.CategoryId is Guid categoryId)
        {
            query = query.Where(p => p.CategoryIds.Contains(categoryId));
        }

        if (criteria.BrandId is Guid brandId)
        {
            query = query.Where(p => p.BrandId == brandId);
        }

        if (criteria.FeaturedOnly is true)
        {
            query = query.Where(p => p.IsFeatured);
        }

        var projected = query.Select(p => new
        {
            p.Id,
            p.Name,
            Slug = p.Slug.Value,
            MinPrice = p.Variants.Min(v => (decimal?)v.Price.Amount),
            MinSalePrice = p.Variants.Where(v => v.SalePrice != null).Select(v => (decimal?)v.SalePrice!.Amount).Min(),
            Currency = p.Variants.Select(v => v.Price.Currency).FirstOrDefault(),
            PrimaryImageUrl = p.Images.Where(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault(),
            Status = p.Status,
        });

        if (criteria.MinPrice is decimal min)
        {
            projected = projected.Where(p => p.MinPrice >= min);
        }

        if (criteria.MaxPrice is decimal max)
        {
            projected = projected.Where(p => p.MinPrice <= max);
        }

        projected = criteria.SortBy switch
        {
            ProductSortOrder.PriceLowToHigh => projected.OrderBy(p => p.MinPrice),
            ProductSortOrder.PriceHighToLow => projected.OrderByDescending(p => p.MinPrice),
            ProductSortOrder.NameAToZ => projected.OrderBy(p => p.Name),
            _ => projected.OrderByDescending(p => p.Id),
        };

        var totalCount = await projected.CountAsync(cancellationToken);

        var items = await projected
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Slug, p.MinPrice, p.MinSalePrice, p.Currency, p.PrimaryImageUrl, p.Status.ToString()))
            .ToListAsync(cancellationToken);

        return new ProductSearchResultDto(items, totalCount, criteria.Page, criteria.PageSize);
    }
}
