using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;
using SharedKernel.ValueObjects;

namespace Shipping.Domain;

/// <summary>
/// Aggregate root for a shipping option (Section: "Shipping methods/zones"). No `ShippingZone`
/// modeling yet — every method applies everywhere; per-country/region rate matching is a real
/// follow-up, not attempted here (scope decision, see docs/decisions.md).
/// </summary>
public sealed class ShippingMethod : AggregateRoot<Guid>
{
    private ShippingMethod(Guid id, string name, string? description, Money cost, int? estimatedDaysMin, int? estimatedDaysMax)
        : base(id)
    {
        Name = name;
        Description = description;
        Cost = cost;
        EstimatedDaysMin = estimatedDaysMin;
        EstimatedDaysMax = estimatedDaysMax;
        IsActive = true;
    }

    private ShippingMethod()
    {
    }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public Money Cost { get; private set; } = null!;

    public int? EstimatedDaysMin { get; private set; }

    public int? EstimatedDaysMax { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<ShippingMethod> Create(
        string name, string? description, decimal cost, string currency, int? estimatedDaysMin, int? estimatedDaysMax)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));

        var costResult = Money.Create(cost, currency);
        if (costResult.IsFailure)
        {
            return Result.Failure<ShippingMethod>(costResult.Error);
        }

        if (estimatedDaysMin is < 0 || estimatedDaysMax is < 0)
        {
            return Result.Failure<ShippingMethod>(Error.Validation("ShippingMethod.InvalidEstimate", "Estimated days cannot be negative."));
        }

        if (estimatedDaysMin is not null && estimatedDaysMax is not null && estimatedDaysMin > estimatedDaysMax)
        {
            return Result.Failure<ShippingMethod>(Error.Validation(
                "ShippingMethod.InvalidEstimateRange", "Minimum estimated days cannot exceed the maximum."));
        }

        return Result.Success(new ShippingMethod(Guid.NewGuid(), name, description, costResult.Value, estimatedDaysMin, estimatedDaysMax));
    }

    public Result UpdateCost(decimal cost, string currency)
    {
        var costResult = Money.Create(cost, currency);
        if (costResult.IsFailure)
        {
            return Result.Failure(costResult.Error);
        }

        Cost = costResult.Value;
        return Result.Success();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
