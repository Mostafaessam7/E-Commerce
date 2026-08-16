using Catalog.Domain.Events;
using Catalog.Domain.ValueObjects;
using SharedKernel.Auditing;
using SharedKernel.Exceptions;
using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;
using SharedKernel.ValueObjects;

namespace Catalog.Domain;

/// <summary>
/// Aggregate root for the catalog. Owns <see cref="ProductVariant"/> (the actual purchasable
/// SKUs — see that type's doc comment) and <see cref="ProductImage"/> as child entities; owns no
/// stock/quantity (Inventory module) and no price *promotion* logic (Promotions module) — those
/// react to this aggregate via integration events rather than being modeled here.
/// </summary>
public sealed class Product : AggregateRoot<Guid>, ISoftDeletableEntity
{
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductImage> _images = [];
    private readonly List<Guid> _categoryIds = [];
    private readonly List<string> _tags = [];
    private readonly List<Guid> _relatedProductIds = [];
    private readonly List<Guid> _crossSellProductIds = [];
    private readonly List<Guid> _upsellProductIds = [];

    private Product(Guid id, string name, Slug slug, string? shortDescription, string? description, Guid? brandId)
        : base(id)
    {
        Name = name;
        Slug = slug;
        ShortDescription = shortDescription;
        Description = description;
        BrandId = brandId;
        Status = ProductStatus.Draft;
        Seo = SeoMetadata.Empty;
    }

    private Product()
    {
    }

    public string Name { get; private set; } = null!;

    public Slug Slug { get; private set; } = null!;

    public string? ShortDescription { get; private set; }

    public string? Description { get; private set; }

    public Guid? BrandId { get; private set; }

    public ProductStatus Status { get; private set; }

    public bool IsFeatured { get; private set; }

    public SeoMetadata Seo { get; private set; } = null!;

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    public IReadOnlyCollection<Guid> CategoryIds => _categoryIds.AsReadOnly();

    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();

    public IReadOnlyCollection<Guid> RelatedProductIds => _relatedProductIds.AsReadOnly();

    public IReadOnlyCollection<Guid> CrossSellProductIds => _crossSellProductIds.AsReadOnly();

    public IReadOnlyCollection<Guid> UpsellProductIds => _upsellProductIds.AsReadOnly();

    public static Result<Product> Create(
        string name,
        string slug,
        string? shortDescription,
        string? description,
        Guid? brandId,
        IEnumerable<Guid>? categoryIds = null)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));

        var slugResult = Slug.FromText(slug);
        if (slugResult.IsFailure)
        {
            return Result.Failure<Product>(slugResult.Error);
        }

        var product = new Product(Guid.NewGuid(), name, slugResult.Value, shortDescription, description, brandId);

        if (categoryIds is not null)
        {
            product._categoryIds.AddRange(categoryIds.Distinct());
        }

        product.RaiseDomainEvent(new ProductCreatedDomainEvent(product.Id));
        return Result.Success(product);
    }

    public Result<Guid> AddVariant(
        string sku,
        decimal price,
        string currency,
        decimal? salePrice,
        string? barcode,
        decimal? weightKg,
        IEnumerable<VariantOption>? options = null)
    {
        Guard.Against.NullOrWhiteSpace(sku, nameof(sku));

        if (_variants.Any(v => v.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<Guid>(Error.Conflict("Product.DuplicateSku", $"SKU '{sku}' already exists on this product."));
        }

        var priceResult = Money.Create(price, currency);
        if (priceResult.IsFailure)
        {
            return Result.Failure<Guid>(priceResult.Error);
        }

        Money? saleMoney = null;
        if (salePrice is not null)
        {
            var saleResult = Money.Create(salePrice.Value, currency);
            if (saleResult.IsFailure)
            {
                return Result.Failure<Guid>(saleResult.Error);
            }

            if (saleResult.Value.Amount > priceResult.Value.Amount)
            {
                return Result.Failure<Guid>(Error.Validation("Product.InvalidSalePrice", "Sale price cannot exceed the regular price."));
            }

            saleMoney = saleResult.Value;
        }

        var variant = new ProductVariant(
            Guid.NewGuid(), sku, priceResult.Value, saleMoney, barcode, weightKg, options ?? []);

        _variants.Add(variant);
        return Result.Success(variant.Id);
    }

    public Result ChangeVariantPrice(Guid variantId, decimal price, string currency, decimal? salePrice)
    {
        var variant = _variants.FirstOrDefault(v => v.Id == variantId);
        if (variant is null)
        {
            return Result.Failure(Error.NotFound("Product.VariantNotFound", "Variant was not found on this product."));
        }

        var priceResult = Money.Create(price, currency);
        if (priceResult.IsFailure)
        {
            return Result.Failure(priceResult.Error);
        }

        Money? saleMoney = null;
        if (salePrice is not null)
        {
            var saleResult = Money.Create(salePrice.Value, currency);
            if (saleResult.IsFailure)
            {
                return Result.Failure(saleResult.Error);
            }

            saleMoney = saleResult.Value;
        }

        variant.ChangePrice(priceResult.Value, saleMoney);
        return Result.Success();
    }

    public void AddImage(string url, string? altText = null, bool isPrimary = false)
    {
        Guard.Against.NullOrWhiteSpace(url, nameof(url));

        if (isPrimary)
        {
            foreach (var existing in _images)
            {
                existing.MakeSecondary();
            }
        }

        _images.Add(new ProductImage(Guid.NewGuid(), url, altText, _images.Count, isPrimary));
    }

    public void SetSeo(string? metaTitle, string? metaDescription, string? canonicalUrl = null) =>
        Seo = SeoMetadata.Create(metaTitle, metaDescription, canonicalUrl);

    public void SetTags(IEnumerable<string> tags)
    {
        _tags.Clear();
        _tags.AddRange(tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public void SetRelatedProducts(IEnumerable<Guid> productIds) => ReplaceIds(_relatedProductIds, productIds);

    public void SetCrossSellProducts(IEnumerable<Guid> productIds) => ReplaceIds(_crossSellProductIds, productIds);

    public void SetUpsellProducts(IEnumerable<Guid> productIds) => ReplaceIds(_upsellProductIds, productIds);

    public void AddCategory(Guid categoryId)
    {
        if (!_categoryIds.Contains(categoryId))
        {
            _categoryIds.Add(categoryId);
        }
    }

    public void RemoveCategory(Guid categoryId) => _categoryIds.Remove(categoryId);

    public void Feature() => IsFeatured = true;

    public void Unfeature() => IsFeatured = false;

    public Result Publish()
    {
        if (_variants.Count == 0)
        {
            return Result.Failure(Error.Validation("Product.NoVariants", "A product must have at least one variant before it can be published."));
        }

        Status = ProductStatus.Active;
        RaiseDomainEvent(new ProductPublishedDomainEvent(Id));
        return Result.Success();
    }

    public void Archive() => Status = ProductStatus.Archived;

    public void Delete(DateTime deletedAtUtc, string deletedBy)
    {
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
        DeletedBy = deletedBy;
    }

    private static void ReplaceIds(List<Guid> target, IEnumerable<Guid> ids)
    {
        target.Clear();
        target.AddRange(ids.Distinct());
    }
}
