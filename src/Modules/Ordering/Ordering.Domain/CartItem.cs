using SharedKernel.Primitives;
using SharedKernel.ValueObjects;

namespace Ordering.Domain;

/// <summary>
/// One line in a cart. <see cref="UnitPrice"/> is the price *at the time the item was added* —
/// display-only until checkout, which always re-fetches the current price from Catalog
/// (Section 6: never trust a stale cart price). Not the same object as <see cref="OrderItem"/>:
/// that one is the permanent, immutable snapshot taken when the order is actually placed.
/// </summary>
public sealed class CartItem : Entity<Guid>
{
    internal CartItem(
        Guid id, Guid productVariantId, Guid productId, string productName, string sku, Money unitPrice,
        int quantity, string? imageUrl = null)
        : base(id)
    {
        ProductVariantId = productVariantId;
        ProductId = productId;
        ProductName = productName;
        Sku = sku;
        UnitPrice = unitPrice;
        Quantity = quantity;
        ImageUrl = imageUrl;
    }

    private CartItem()
    {
    }

    public Guid ProductVariantId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = null!;

    public string Sku { get; private set; } = null!;

    /// <summary>
    /// The product's primary image URL *at the time this item was added* — display-only, same
    /// staleness rule as <see cref="UnitPrice"/> (Section 6: never trust a stale cart value for
    /// anything but display). Nullable: a product can have no images yet (Phase 4/29).
    /// </summary>
    public string? ImageUrl { get; private set; }

    public Money UnitPrice { get; private set; } = null!;

    public int Quantity { get; private set; }

    public Money LineTotal => UnitPrice.Multiply(Quantity);

    internal void ChangeQuantity(int quantity) => Quantity = quantity;

    internal void RefreshPrice(Money unitPrice) => UnitPrice = unitPrice;
}
