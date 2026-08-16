using FluentAssertions;
using SharedKernel.ValueObjects;

namespace UnitTests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Create_succeeds_for_a_valid_amount_and_currency()
    {
        var result = Money.Create(100m, "usd");

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(100m);
        result.Value.Currency.Should().Be("USD", "currency codes are normalized to upper-case");
    }

    [Fact]
    public void Create_fails_for_a_negative_amount()
    {
        var result = Money.Create(-1m, "USD");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Money.NegativeAmount");
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Create_fails_for_an_invalid_currency_code(string currency)
    {
        var result = Money.Create(10m, currency);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Money.InvalidCurrency");
    }

    [Fact]
    public void Two_amounts_in_the_same_currency_are_added_together()
    {
        var first = Money.Create(10m, "EGP").Value;
        var second = Money.Create(5.50m, "EGP").Value;

        var sum = first + second;

        sum.Amount.Should().Be(15.50m);
        sum.Currency.Should().Be("EGP");
    }

    [Fact]
    public void Adding_amounts_in_different_currencies_throws()
    {
        var egp = Money.Create(10m, "EGP").Value;
        var usd = Money.Create(10m, "USD").Value;

        var act = () => egp + usd;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Money_with_the_same_amount_and_currency_are_equal()
    {
        var first = Money.Create(10m, "EGP").Value;
        var second = Money.Create(10m, "EGP").Value;

        first.Should().Be(second);
    }

    [Fact]
    public void Zero_creates_a_zero_amount_in_the_given_currency()
    {
        var zero = Money.Zero("EGP");

        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be("EGP");
    }
}
