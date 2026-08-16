namespace SharedKernel.ValueObjects;

/// <summary>
/// Base for immutable objects compared by the value of their components, not by identity —
/// e.g. <see cref="Money"/>, an Address, a Slug. Derive, implement
/// <see cref="GetEqualityComponents"/> with every field that participates in equality, and
/// Equals/GetHashCode follow for free.
///
/// Deliberately does NOT overload <c>==</c>/<c>!=</c>. A value object is frequently also an EF
/// Core value-converted scalar property (e.g. <c>Catalog.Domain.ValueObjects.Slug</c> mapped
/// string&lt;-&gt;Slug). EF Core's LINQ provider translates a converted property compared with
/// <c>Expression.Equal</c> — the node shape produced by comparing two objects of a type with NO
/// custom equality operator — straight to a SQL column comparison, applying the same converter to
/// the constant/parameter on the other side. An overloaded <c>==</c> instead compiles to a
/// <c>MethodCallExpression</c> (<c>op_Equality</c>), which EF Core cannot translate and throws on.
/// Use <c>.Equals(...)</c> for value comparisons in C# code; it isn't affected by this.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null || GetType() != other.GetType())
        {
            return false;
        }

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) => Equals(obj as ValueObject);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }
}
