using Messaging;

namespace Shipping.Contracts;

public sealed record ShippingMethodDto(
    Guid Id, string Name, string? Description, decimal Cost, string Currency,
    int? EstimatedDaysMin, int? EstimatedDaysMax, bool IsActive);

/// <summary>For checkout's method picker — active methods only unless
/// <paramref name="IncludeInactive"/> (admin listing).</summary>
public sealed record ListShippingMethodsQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<ShippingMethodDto>>;

/// <summary>
/// Dispatched from Ordering's checkout (ADR-014) to get the authoritative cost for whichever
/// method the customer picked — never trust a client-submitted shipping cost, same rule already
/// applied to price (Catalog) and stock (Inventory).
/// </summary>
public sealed record GetShippingMethodQuery(Guid ShippingMethodId) : IQuery<ShippingMethodDto>;
