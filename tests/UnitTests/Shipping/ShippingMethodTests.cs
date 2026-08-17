using FluentAssertions;
using Shipping.Domain;

namespace UnitTests.Shipping;

public class ShippingMethodTests
{
    [Fact]
    public void Create_succeeds_with_a_valid_name_and_cost()
    {
        var result = ShippingMethod.Create("Standard", "3-5 business days", 30m, "EGP", 3, 5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Standard");
        result.Value.Cost.Amount.Should().Be(30m);
        result.Value.IsActive.Should().BeTrue("a newly created shipping method is active by default");
    }

    [Fact]
    public void Create_throws_for_a_blank_name()
    {
        var act = () => ShippingMethod.Create("   ", null, 30m, "EGP", null, null);

        act.Should().Throw<ArgumentException>("Guard.Against.NullOrWhiteSpace enforces this as an unreachable invariant, not an expected failure");
    }

    [Fact]
    public void Create_fails_when_the_minimum_estimate_exceeds_the_maximum()
    {
        var result = ShippingMethod.Create("Express", null, 60m, "EGP", 5, 2);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShippingMethod.InvalidEstimateRange");
    }

    [Fact]
    public void Create_fails_for_a_negative_estimate()
    {
        var result = ShippingMethod.Create("Express", null, 60m, "EGP", -1, 2);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShippingMethod.InvalidEstimate");
    }

    [Fact]
    public void UpdateCost_changes_the_cost_in_place()
    {
        var method = ShippingMethod.Create("Standard", null, 30m, "EGP", null, null).Value;

        var result = method.UpdateCost(45m, "EGP");

        result.IsSuccess.Should().BeTrue();
        method.Cost.Amount.Should().Be(45m);
    }

    [Fact]
    public void Deactivate_then_activate_toggles_availability()
    {
        var method = ShippingMethod.Create("Standard", null, 30m, "EGP", null, null).Value;

        method.Deactivate();
        method.IsActive.Should().BeFalse();

        method.Activate();
        method.IsActive.Should().BeTrue();
    }
}
