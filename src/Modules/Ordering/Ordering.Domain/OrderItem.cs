using SharedKernel.Primitives;
using SharedKernel.ValueObjects;

namespace Ordering.Domain;

/// <summary>
/// Permanent, immutable snapshot of a purchased line — Section 27's explicit requirement:
/// changing a Product's price later must never change what a past Order shows/owes. Captured
/// once, at <see cref="Order.Place"/>, never updated afterward.
/// </summary>
public sealed class OrderItem : Entity<Guid>
{
    internal OrderItem(Guid id, Guid productVariantId, Guid productId, string productName, string sku, Money unitPrice, int quantity)
        : base(id)
    {
        ProductVariantId = productVariantId;
        ProductId = productId;
        ProductName = productName;
        Sku = sku;
        UnitPrice = unitPrice;
        Quantity = quantity;
        LineTotal = unitPrice.Multiply(quantity);
    }

    private OrderItem()
    {
    }

    public Guid ProductVariantId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = null!;

    public string Sku { get; private set; } = null!;

    public Money UnitPrice { get; private set; } = null!;

    public int Quantity { get; private set; }

    public Money LineTotal { get; private set; } = null!;
}
