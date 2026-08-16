using FluentAssertions;
using SharedKernel.Guards;

namespace UnitTests.Guards;

public class GuardTests
{
    [Fact]
    public void Null_throws_when_value_is_null()
    {
        string? value = null;

        var act = () => Guard.Against.Null(value, nameof(value));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Null_returns_the_value_when_not_null()
    {
        var value = "hello";

        var result = Guard.Against.Null(value, nameof(value));

        result.Should().Be("hello");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrWhiteSpace_throws_for_blank_input(string? value)
    {
        var act = () => Guard.Against.NullOrWhiteSpace(value, nameof(value));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Empty_throws_for_an_empty_guid()
    {
        var act = () => Guard.Against.Empty(Guid.Empty, "id");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Negative_throws_for_a_negative_decimal()
    {
        var act = () => Guard.Against.Negative(-0.01m, "amount");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NegativeOrZero_throws_for_zero()
    {
        var act = () => Guard.Against.NegativeOrZero(0, "quantity");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
