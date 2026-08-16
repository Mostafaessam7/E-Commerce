using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;

namespace Catalog.Domain;

/// <summary>
/// A variant axis (e.g. "Color", "Size") and its allowed values — the flexible alternative to
/// hardcoding Size/Color columns on Product (Section 4's explicit requirement).
/// <see cref="AttributeValue"/> is a child entity, not its own aggregate: values only ever make
/// sense in the context of their attribute.
/// </summary>
public sealed class ProductAttribute : AggregateRoot<Guid>
{
    private readonly List<AttributeValue> _values = [];

    private ProductAttribute(Guid id, string name)
        : base(id)
    {
        Name = name;
    }

    private ProductAttribute()
    {
    }

    public string Name { get; private set; } = null!;

    public IReadOnlyCollection<AttributeValue> Values => _values.AsReadOnly();

    public static Result<ProductAttribute> Create(string name)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        return Result.Success(new ProductAttribute(Guid.NewGuid(), name));
    }

    public Result<AttributeValue> AddValue(string value, int displayOrder = 0)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));

        if (_values.Any(v => v.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<AttributeValue>(SharedKernel.Results.Error.Conflict(
                "ProductAttribute.DuplicateValue", $"Value '{value}' already exists for attribute '{Name}'."));
        }

        var attributeValue = new AttributeValue(Guid.NewGuid(), Id, value, displayOrder);
        _values.Add(attributeValue);
        return Result.Success(attributeValue);
    }
}

public sealed class AttributeValue : Entity<Guid>
{
    internal AttributeValue(Guid id, Guid attributeId, string value, int displayOrder)
        : base(id)
    {
        AttributeId = attributeId;
        Value = value;
        DisplayOrder = displayOrder;
    }

    private AttributeValue()
    {
    }

    public Guid AttributeId { get; private set; }

    public string Value { get; private set; } = null!;

    public int DisplayOrder { get; private set; }
}
