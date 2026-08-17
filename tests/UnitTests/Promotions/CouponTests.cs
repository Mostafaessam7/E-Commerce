using FluentAssertions;
using Promotions.Domain;

namespace UnitTests.Promotions;

public class CouponTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Redeem_computes_a_percentage_discount_off_the_order_amount()
    {
        var coupon = Coupon.Create("SAVE10", DiscountType.Percentage, 10m, "EGP", null, null, null).Value;

        var result = coupon.Redeem(200m, "EGP", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(20m);
        coupon.UsageCount.Should().Be(1);
    }

    [Fact]
    public void Redeem_caps_a_fixed_amount_discount_at_the_order_amount()
    {
        var coupon = Coupon.Create("FLAT50", DiscountType.FixedAmount, 50m, "EGP", null, null, null).Value;

        var result = coupon.Redeem(30m, "EGP", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(30m, "a fixed discount must never exceed the order amount it's applied to");
    }

    [Fact]
    public void Redeem_fails_once_the_usage_limit_is_reached()
    {
        var coupon = Coupon.Create("ONEUSE", DiscountType.Percentage, 10m, "EGP", null, usageLimit: 1, null).Value;
        coupon.Redeem(100m, "EGP", Now);

        var result = coupon.Redeem(100m, "EGP", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Coupon.UsageLimitReached");
    }

    [Fact]
    public void Redeem_fails_for_an_expired_coupon()
    {
        var coupon = Coupon.Create("EXPIRED", DiscountType.Percentage, 10m, "EGP", Now.AddDays(-1), null, null).Value;

        var result = coupon.Redeem(100m, "EGP", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Coupon.Expired");
    }

    [Fact]
    public void Redeem_fails_when_the_order_amount_is_below_the_minimum()
    {
        var coupon = Coupon.Create("MIN100", DiscountType.Percentage, 10m, "EGP", null, null, minimumOrderAmount: 100m).Value;

        var result = coupon.Redeem(50m, "EGP", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Coupon.MinimumOrderAmountNotMet");
    }

    [Fact]
    public void Redeem_fails_for_a_deactivated_coupon()
    {
        var coupon = Coupon.Create("OFF", DiscountType.Percentage, 10m, "EGP", null, null, null).Value;
        coupon.Deactivate();

        var result = coupon.Redeem(100m, "EGP", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Coupon.Inactive");
    }

    [Fact]
    public void ReleaseRedemption_decrements_usage_count_so_a_failed_order_does_not_burn_a_use()
    {
        var coupon = Coupon.Create("SAVE10", DiscountType.Percentage, 10m, "EGP", null, usageLimit: 1, null).Value;
        coupon.Redeem(100m, "EGP", Now);

        coupon.ReleaseRedemption();

        coupon.UsageCount.Should().Be(0);
        coupon.Redeem(100m, "EGP", Now).IsSuccess.Should().BeTrue("the usage slot must be available again after release");
    }
}
