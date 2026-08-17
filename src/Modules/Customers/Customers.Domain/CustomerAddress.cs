using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;

namespace Customers.Domain;

/// <summary>A saved, reusable address in the customer's address book — distinct from
/// <c>Ordering.Domain.ValueObjects.Address</c>, which is a permanent snapshot on a placed order
/// (Section: later edits to a saved address must never retroactively change a past order).</summary>
public sealed class CustomerAddress : Entity<Guid>
{
    internal CustomerAddress(
        Guid id, string label, string fullName, string phone, string line1, string? line2,
        string city, string? state, string postalCode, string country, bool isDefault)
        : base(id)
    {
        Label = label;
        FullName = fullName;
        Phone = phone;
        Line1 = line1;
        Line2 = line2;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
        IsDefault = isDefault;
    }

    private CustomerAddress()
    {
    }

    public string Label { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public string Phone { get; private set; } = null!;

    public string Line1 { get; private set; } = null!;

    public string? Line2 { get; private set; }

    public string City { get; private set; } = null!;

    public string? State { get; private set; }

    public string PostalCode { get; private set; } = null!;

    public string Country { get; private set; } = null!;

    public bool IsDefault { get; private set; }

    internal void MakeDefault() => IsDefault = true;

    internal void MakeNonDefault() => IsDefault = false;

    internal static Result<CustomerAddress> Create(
        string label, string fullName, string phone, string line1, string? line2,
        string city, string? state, string postalCode, string country, bool isDefault)
    {
        Guard.Against.NullOrWhiteSpace(label, nameof(label));
        Guard.Against.NullOrWhiteSpace(fullName, nameof(fullName));
        Guard.Against.NullOrWhiteSpace(phone, nameof(phone));
        Guard.Against.NullOrWhiteSpace(line1, nameof(line1));
        Guard.Against.NullOrWhiteSpace(city, nameof(city));
        Guard.Against.NullOrWhiteSpace(postalCode, nameof(postalCode));
        Guard.Against.NullOrWhiteSpace(country, nameof(country));

        return Result.Success(new CustomerAddress(
            Guid.NewGuid(), label, fullName, phone, line1, line2, city, state, postalCode, country, isDefault));
    }
}
