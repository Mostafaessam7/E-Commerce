using SharedKernel.Guards;
using SharedKernel.Results;
using SharedKernel.ValueObjects;

namespace Ordering.Domain.ValueObjects;

/// <summary>Billing/shipping address — a snapshot captured at checkout time (Section 27:
/// never re-resolved from a live "Customer address book" row, so an order's shipping label
/// stays correct even if the customer edits their saved addresses later).</summary>
public sealed class Address : ValueObject
{
    private Address(string fullName, string phone, string line1, string? line2, string city, string? state, string postalCode, string country)
    {
        FullName = fullName;
        Phone = phone;
        Line1 = line1;
        Line2 = line2;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public string FullName { get; }

    public string Phone { get; }

    public string Line1 { get; }

    public string? Line2 { get; }

    public string City { get; }

    public string? State { get; }

    public string PostalCode { get; }

    public string Country { get; }

    public static Result<Address> Create(
        string fullName, string phone, string line1, string? line2, string city, string? state, string postalCode, string country)
    {
        Guard.Against.NullOrWhiteSpace(fullName, nameof(fullName));
        Guard.Against.NullOrWhiteSpace(phone, nameof(phone));
        Guard.Against.NullOrWhiteSpace(line1, nameof(line1));
        Guard.Against.NullOrWhiteSpace(city, nameof(city));
        Guard.Against.NullOrWhiteSpace(postalCode, nameof(postalCode));
        Guard.Against.NullOrWhiteSpace(country, nameof(country));

        return Result.Success(new Address(fullName, phone, line1, line2, city, state, postalCode, country));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FullName;
        yield return Phone;
        yield return Line1;
        yield return Line2;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }
}
