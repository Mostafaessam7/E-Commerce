namespace Ordering.Application.Carts;

internal static class CartMapper
{
    public static CartDto ToDto(Domain.Cart cart)
    {
        var currency = cart.Items.Count > 0 ? cart.Items.First().UnitPrice.Currency : "EGP";

        return new CartDto(
            cart.Id,
            cart.CustomerId,
            cart.AnonymousId,
            cart.CouponCode,
            cart.Items.Select(i => new CartItemDto(i.Id, i.ProductVariantId, i.ProductName, i.Sku, i.UnitPrice.Amount, i.Quantity, i.LineTotal.Amount, i.ImageUrl)).ToList(),
            cart.Subtotal.Amount,
            currency);
    }
}
