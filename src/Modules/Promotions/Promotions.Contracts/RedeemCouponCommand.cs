using Messaging;

namespace Promotions.Contracts;

/// <summary>
/// Dispatched from Ordering's checkout (ADR-014) — the cart only ever stores a coupon *code*
/// string (`Ordering.Domain.Cart.ApplyCoupon`); this is where it's actually validated and its
/// discount computed, the same "never trust the cart's stale snapshot" rule
/// `PlaceOrderCommandHandler` already applies to price and stock. Increments the coupon's usage
/// count as a side effect — if the order this was redeemed for doesn't end up placing, the caller
/// must dispatch <see cref="ReleaseCouponCommand"/> to undo that (mirrors
/// `Inventory.Contracts.ReserveStockCommand`/`ReleaseStockCommand`'s compensation pattern).
/// </summary>
public sealed record RedeemCouponCommand(string Code, decimal OrderAmount, string Currency) : ICommand<decimal>;

/// <summary>Compensating action for a <see cref="RedeemCouponCommand"/> whose order ultimately
/// failed to place — see that record's doc comment.</summary>
public sealed record ReleaseCouponCommand(string Code) : ICommand<Unit>;
