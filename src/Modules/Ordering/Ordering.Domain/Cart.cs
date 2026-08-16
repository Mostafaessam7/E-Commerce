using SharedKernel.Exceptions;
using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;
using SharedKernel.ValueObjects;

namespace Ordering.Domain;

/// <summary>
/// Guest or authenticated-customer cart (Section 6). Exactly one of <see cref="CustomerId"/> /
/// <see cref="AnonymousId"/> is set — a guest cart is identified by an anonymous id (a long-lived
/// cookie value from Store.Web), and <see cref="MergeFrom"/> is how it becomes a customer's cart
/// at login (Section 6: "Merge Guest Cart after Login").
/// </summary>
public sealed class Cart : AggregateRoot<Guid>
{
    private readonly List<CartItem> _items = [];

    private Cart(Guid id, Guid? customerId, Guid? anonymousId)
        : base(id)
    {
        CustomerId = customerId;
        AnonymousId = anonymousId;
    }

    private Cart()
    {
    }

    public Guid? CustomerId { get; private set; }

    public Guid? AnonymousId { get; private set; }

    public string? CouponCode { get; private set; }

    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    public Money Subtotal => _items
        .Select(i => i.LineTotal)
        .Aggregate((Money?)null, (total, line) => total is null ? line : total.Add(line))
        ?? Money.Zero("EGP");

    public static Cart CreateForCustomer(Guid customerId)
    {
        Guard.Against.Empty(customerId, nameof(customerId));
        return new Cart(Guid.NewGuid(), customerId, null);
    }

    public static Cart CreateForGuest(Guid anonymousId)
    {
        Guard.Against.Empty(anonymousId, nameof(anonymousId));
        return new Cart(Guid.NewGuid(), null, anonymousId);
    }

    public Result AddItem(Guid productVariantId, Guid productId, string productName, string sku, decimal price, string currency, int quantity)
    {
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        var priceResult = Money.Create(price, currency);
        if (priceResult.IsFailure)
        {
            return Result.Failure(priceResult.Error);
        }

        var existing = _items.FirstOrDefault(i => i.ProductVariantId == productVariantId);
        if (existing is not null)
        {
            existing.ChangeQuantity(existing.Quantity + quantity);
            existing.RefreshPrice(priceResult.Value);
        }
        else
        {
            _items.Add(new CartItem(Guid.NewGuid(), productVariantId, productId, productName, sku, priceResult.Value, quantity));
        }

        return Result.Success();
    }

    public Result RemoveItem(Guid cartItemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == cartItemId);
        if (item is null)
        {
            return Result.Failure(Error.NotFound("Cart.ItemNotFound", "Cart item was not found."));
        }

        _items.Remove(item);
        return Result.Success();
    }

    public Result ChangeItemQuantity(Guid cartItemId, int quantity)
    {
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        var item = _items.FirstOrDefault(i => i.Id == cartItemId);
        if (item is null)
        {
            return Result.Failure(Error.NotFound("Cart.ItemNotFound", "Cart item was not found."));
        }

        item.ChangeQuantity(quantity);
        return Result.Success();
    }

    public void ApplyCoupon(string code) => CouponCode = Guard.Against.NullOrWhiteSpace(code, nameof(code));

    public void RemoveCoupon() => CouponCode = null;

    public void Clear() => _items.Clear();

    /// <summary>Copies every line from a guest cart into this (now-authenticated) cart, merging
    /// quantities for variants already present, then empties the guest cart. Called once, at
    /// login — see docs/events.md for where this is triggered from.</summary>
    public void MergeFrom(Cart guestCart)
    {
        if (guestCart.Id == Id)
        {
            throw new DomainException("A cart cannot be merged into itself.");
        }

        foreach (var item in guestCart.Items)
        {
            AddItem(item.ProductVariantId, item.ProductId, item.ProductName, item.Sku, item.UnitPrice.Amount, item.UnitPrice.Currency, item.Quantity);
        }

        guestCart.Clear();
    }
}
