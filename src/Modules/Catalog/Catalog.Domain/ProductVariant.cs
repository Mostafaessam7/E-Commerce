using Catalog.Domain.ValueObjects;
using SharedKernel.Exceptions;
using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.ValueObjects;

namespace Catalog.Domain;

/// <summary>
/// One purchasable SKU of a Product — Section 4's "Black / M", "Black / L" example. Stock is
/// deliberately not here: Inventory owns quantity/reservation, keyed by this variant's
/// <see cref="Id"/> (a plain Guid reference, never a cross-module FK/navigation — see
/// docs/architecture.md's module communication rules).
/// </summary>
public sealed class ProductVariant : Entity<Guid>
{
    private readonly List<VariantOption> _options = [];

    internal ProductVariant(
        Guid id,
        string sku,
        Money price,
        Money? salePrice,
        string? barcode,
        decimal? weightKg,
        IEnumerable<VariantOption> options)
        : base(id)
    {
        Sku = sku;
        Price = price;
        SalePrice = salePrice;
        Barcode = barcode;
        WeightKg = weightKg;
        _options.AddRange(options);
    }

    private ProductVariant()
    {
    }

    public string Sku { get; private set; } = null!;

    public string? Barcode { get; private set; }

    public Money Price { get; private set; } = null!;

    public Money? SalePrice { get; private set; }

    public decimal? WeightKg { get; private set; }

    public string? ImageUrl { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<VariantOption> Options => _options.AsReadOnly();

    internal void ChangePrice(Money price, Money? salePrice)
    {
        if (salePrice is not null && salePrice.Amount > price.Amount)
        {
            throw new DomainException("Sale price cannot exceed the regular price.");
        }

        Price = price;
        SalePrice = salePrice;
    }

    internal void SetImage(string? imageUrl) => ImageUrl = imageUrl;

    internal void Activate() => IsActive = true;

    internal void Deactivate() => IsActive = false;
}
