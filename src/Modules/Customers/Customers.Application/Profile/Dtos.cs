namespace Customers.Application.Profile;

public sealed record CustomerAddressDto(
    Guid Id, string Label, string FullName, string Phone, string Line1, string? Line2,
    string City, string? State, string PostalCode, string Country, bool IsDefault);

public sealed record CustomerProfileDto(
    Guid Id, string Email, string? FullName, string? Phone, IReadOnlyList<CustomerAddressDto> Addresses);
