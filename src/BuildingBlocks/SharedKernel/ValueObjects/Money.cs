using SharedKernel.Results;

namespace SharedKernel.ValueObjects;

/// <summary>
/// The only representation of a monetary amount anywhere in the system — never a bare
/// <c>decimal</c>, and never <c>double</c> (binary floating point loses cents). Shared here
/// rather than duplicated per module because Catalog (prices), Ordering (line items, totals),
/// Payments (charges, refunds) and Promotions (discounts) all need the exact same "amount +
/// currency, arithmetic only within the same currency" semantics.
///
/// <see cref="Currency"/> is a 3-letter ISO 4217 code (e.g. "EGP", "USD"). Rounding/precision
/// per currency (e.g. JPY has no minor unit) is intentionally out of scope for Phase 1 — the
/// storefront ships single-currency first; a <c>CurrencyInfo</c> lookup can be added the day a
/// second currency is real requirement instead of speculative now.
/// </summary>
public sealed class Money : ValueObject
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero(string currency) => new(0m, Normalize(currency));

    public static Result<Money> Create(decimal amount, string currency)
    {
        if (amount < 0)
        {
            return Result.Failure<Money>(Error.Validation(
                "Money.NegativeAmount",
                "A monetary amount cannot be negative."));
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            return Result.Failure<Money>(Error.Validation(
                "Money.InvalidCurrency",
                "Currency must be a 3-letter ISO 4217 code."));
        }

        return Result.Success(new Money(amount, Normalize(currency)));
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator *(Money money, decimal factor) => money.Multiply(factor);

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine amounts in different currencies ('{Currency}' and '{other.Currency}').");
        }
    }

    private static string Normalize(string currency) => currency.Trim().ToUpperInvariant();

    public override string ToString() => $"{Amount:0.00} {Currency}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
