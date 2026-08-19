namespace Ordering.Application.Carts;

public sealed record CartItemDto(Guid Id, Guid ProductVariantId, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal, string? ImageUrl);

public sealed record CartDto(Guid Id, Guid? CustomerId, Guid? AnonymousId, string? CouponCode, IReadOnlyList<CartItemDto> Items, decimal Subtotal, string Currency);
