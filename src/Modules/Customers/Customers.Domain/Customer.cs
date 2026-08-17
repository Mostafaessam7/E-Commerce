using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;

namespace Customers.Domain;

/// <summary>
/// Aggregate root for a customer's profile + saved address book — distinct from Identity's
/// <c>ApplicationUser</c> (auth/credentials) by design (Section: Customers doesn't own
/// authentication). <see cref="Id"/> is deliberately the *same* Guid as the owning
/// `ApplicationUser.Id`, not a fresh one — a 1:1 relationship with no need for its own foreign
/// key column, and no cross-module DB reference either (Identity and Customers still never touch
/// each other's tables — Store.Web's controller is the only thing that knows both ids are equal).
/// </summary>
public sealed class Customer : AggregateRoot<Guid>
{
    private readonly List<CustomerAddress> _addresses = [];

    private Customer(Guid id, string email)
        : base(id)
    {
        Email = email;
    }

    private Customer()
    {
    }

    /// <summary>Cached for display only — Identity remains the source of truth for the actual
    /// login email; this module never validates or changes it.</summary>
    public string Email { get; private set; } = null!;

    public string? FullName { get; private set; }

    public string? Phone { get; private set; }

    public IReadOnlyCollection<CustomerAddress> Addresses => _addresses.AsReadOnly();

    public static Result<Customer> Create(Guid id, string email)
    {
        Guard.Against.Empty(id, nameof(id));
        Guard.Against.NullOrWhiteSpace(email, nameof(email));

        return Result.Success(new Customer(id, email));
    }

    public void UpdateProfile(string? fullName, string? phone)
    {
        FullName = fullName;
        Phone = phone;
    }

    public Result<Guid> AddAddress(
        string label, string fullName, string phone, string line1, string? line2,
        string city, string? state, string postalCode, string country, bool isDefault)
    {
        var addressResult = CustomerAddress.Create(label, fullName, phone, line1, line2, city, state, postalCode, country, isDefault: false);
        if (addressResult.IsFailure)
        {
            return Result.Failure<Guid>(addressResult.Error);
        }

        var address = addressResult.Value;
        _addresses.Add(address);

        // First address ever added, or explicitly requested — either way, exactly one address is
        // ever marked default (SetDefaultAddress enforces this too).
        if (isDefault || _addresses.Count == 1)
        {
            SetDefaultAddressInternal(address.Id);
        }

        return Result.Success(address.Id);
    }

    public Result RemoveAddress(Guid addressId)
    {
        var address = _addresses.FirstOrDefault(a => a.Id == addressId);
        if (address is null)
        {
            return Result.Failure(Error.NotFound("Customer.AddressNotFound", "Address was not found."));
        }

        var wasDefault = address.IsDefault;
        _addresses.Remove(address);

        // Never leave the book with addresses but no default — the checkout pre-fill (Store.Web)
        // always looks for "the" default; silently having none would just mean it degrades to
        // asking the customer to type the address again, but there's no reason to allow that
        // when a perfectly good next candidate already exists.
        if (wasDefault && _addresses.Count > 0)
        {
            SetDefaultAddressInternal(_addresses[0].Id);
        }

        return Result.Success();
    }

    public Result SetDefaultAddress(Guid addressId)
    {
        if (_addresses.All(a => a.Id != addressId))
        {
            return Result.Failure(Error.NotFound("Customer.AddressNotFound", "Address was not found."));
        }

        SetDefaultAddressInternal(addressId);
        return Result.Success();
    }

    private void SetDefaultAddressInternal(Guid addressId)
    {
        foreach (var address in _addresses)
        {
            if (address.Id == addressId)
            {
                address.MakeDefault();
            }
            else
            {
                address.MakeNonDefault();
            }
        }
    }
}
