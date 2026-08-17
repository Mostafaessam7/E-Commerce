using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;

namespace Promotions.Domain;

/// <summary>
/// Aggregate root for a discount coupon (Section: "Coupons, discount rules"). Ordering never
/// trusts the cart's stored coupon code at face value — <see cref="Redeem"/> is dispatched from
/// checkout (ADR-014, `Promotions.Contracts.RedeemCouponCommand`) the same way price/stock get
/// re-validated, and is the only place a coupon's usage count actually increments (applying it to
/// a cart, `Ordering.Domain.Cart.ApplyCoupon`, just stores the code string — no validation
/// happens until checkout, same "never trust the cart's stale snapshot" rule as everything else
/// in `PlaceOrderCommandHandler`).
/// </summary>
public sealed class Coupon : AggregateRoot<Guid>
{
    private Coupon(
        Guid id, string code, DiscountType discountType, decimal value, string currency,
        DateTime? expiresAtUtc, int? usageLimit, decimal? minimumOrderAmount)
        : base(id)
    {
        Code = code;
        DiscountType = discountType;
        Value = value;
        Currency = currency;
        ExpiresAtUtc = expiresAtUtc;
        UsageLimit = usageLimit;
        MinimumOrderAmount = minimumOrderAmount;
        IsActive = true;
        UsageCount = 0;
    }

    private Coupon()
    {
    }

    public string Code { get; private set; } = null!;

    public DiscountType DiscountType { get; private set; }

    /// <summary>Percentage (0-100) or a fixed amount in <see cref="Currency"/>, depending on
    /// <see cref="DiscountType"/>.</summary>
    public decimal Value { get; private set; }

    public string Currency { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public int? UsageLimit { get; private set; }

    public int UsageCount { get; private set; }

    public decimal? MinimumOrderAmount { get; private set; }

    public static Result<Coupon> Create(
        string code, DiscountType discountType, decimal value, string currency,
        DateTime? expiresAtUtc, int? usageLimit, decimal? minimumOrderAmount)
    {
        Guard.Against.NullOrWhiteSpace(code, nameof(code));
        Guard.Against.NullOrWhiteSpace(currency, nameof(currency));

        if (discountType == DiscountType.Percentage && (value <= 0 || value > 100))
        {
            return Result.Failure<Coupon>(Error.Validation("Coupon.InvalidPercentage", "A percentage discount must be between 0 and 100."));
        }

        if (discountType == DiscountType.FixedAmount && value <= 0)
        {
            return Result.Failure<Coupon>(Error.Validation("Coupon.InvalidValue", "A fixed discount amount must be positive."));
        }

        if (usageLimit is <= 0)
        {
            return Result.Failure<Coupon>(Error.Validation("Coupon.InvalidUsageLimit", "Usage limit must be positive when set."));
        }

        return Result.Success(new Coupon(
            Guid.NewGuid(), code.Trim().ToUpperInvariant(), discountType, value, currency,
            expiresAtUtc, usageLimit, minimumOrderAmount));
    }

    /// <summary>Validates the coupon is usable right now against <paramref name="orderAmount"/>
    /// and, if so, computes the discount and increments <see cref="UsageCount"/> — the only
    /// mutation this type has. Never returns a discount larger than the order amount itself (a
    /// fixed-amount coupon on a small order must not produce a negative total).</summary>
    public Result<decimal> Redeem(decimal orderAmount, string currency, DateTime occurredAtUtc)
    {
        if (!IsActive)
        {
            return Result.Failure<decimal>(Error.Conflict("Coupon.Inactive", "This coupon is not active."));
        }

        if (ExpiresAtUtc is not null && occurredAtUtc > ExpiresAtUtc)
        {
            return Result.Failure<decimal>(Error.Conflict("Coupon.Expired", "This coupon has expired."));
        }

        if (!string.Equals(Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<decimal>(Error.Validation("Coupon.CurrencyMismatch", "This coupon is not valid for this currency."));
        }

        if (UsageLimit is not null && UsageCount >= UsageLimit)
        {
            return Result.Failure<decimal>(Error.Conflict("Coupon.UsageLimitReached", "This coupon has reached its usage limit."));
        }

        if (MinimumOrderAmount is decimal minimum && orderAmount < minimum)
        {
            return Result.Failure<decimal>(Error.Validation(
                "Coupon.MinimumOrderAmountNotMet", $"This coupon requires a minimum order amount of {minimum:0.00}."));
        }

        var discount = DiscountType == DiscountType.Percentage
            ? Math.Round(orderAmount * Value / 100m, 2)
            : Math.Min(Value, orderAmount);

        UsageCount++;
        return Result.Success(discount);
    }

    /// <summary>Compensating action (ADR-014's "release" pattern, same shape as Inventory's
    /// `ReleaseStockCommand`) — undoes the usage-count increment from <see cref="Redeem"/> when
    /// the order it was redeemed for ultimately fails to place (e.g. a later stock reservation
    /// failure). Never fails: worst case the count is already back to where it should be.</summary>
    public void ReleaseRedemption()
    {
        if (UsageCount > 0)
        {
            UsageCount--;
        }
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
