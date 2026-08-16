using System.Text.RegularExpressions;
using SharedKernel.Results;
using SharedKernel.ValueObjects;

namespace Catalog.Domain.ValueObjects;

/// <summary>URL-safe identifier used for SEO-friendly product/category/brand URLs (Section 29).</summary>
public sealed partial class Slug : ValueObject
{
    private Slug(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Slug> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Slug>(Error.Validation("Slug.Empty", "Slug cannot be empty."));
        }

        var normalized = SlugPattern().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');

        if (normalized.Length == 0)
        {
            return Result.Failure<Slug>(Error.Validation("Slug.Invalid", "Slug must contain at least one letter or digit."));
        }

        return Result.Success(new Slug(normalized));
    }

    /// <summary>Builds a slug from free text (e.g. a product name), normalizing as it goes.</summary>
    public static Result<Slug> FromText(string text) => Create(text);

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugPattern();
}
