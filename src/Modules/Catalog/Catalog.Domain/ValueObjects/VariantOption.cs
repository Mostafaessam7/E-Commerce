using SharedKernel.ValueObjects;

namespace Catalog.Domain.ValueObjects;

/// <summary>
/// One "axis" value on a variant, e.g. (Color attribute, Red value). A variant carries one of
/// these per attribute it varies by — see Section 4's Nike T-Shirt example (Black/M, Black/L, ...).
/// </summary>
public sealed class VariantOption : ValueObject
{
    private VariantOption(Guid attributeId, Guid attributeValueId)
    {
        AttributeId = attributeId;
        AttributeValueId = attributeValueId;
    }

    public Guid AttributeId { get; }

    public Guid AttributeValueId { get; }

    public static VariantOption Create(Guid attributeId, Guid attributeValueId) => new(attributeId, attributeValueId);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AttributeId;
        yield return AttributeValueId;
    }
}
