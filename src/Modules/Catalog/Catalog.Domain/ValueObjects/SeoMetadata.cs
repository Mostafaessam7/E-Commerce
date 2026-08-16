using SharedKernel.ValueObjects;

namespace Catalog.Domain.ValueObjects;

public sealed class SeoMetadata : ValueObject
{
    private SeoMetadata(string? metaTitle, string? metaDescription, string? canonicalUrl)
    {
        MetaTitle = metaTitle;
        MetaDescription = metaDescription;
        CanonicalUrl = canonicalUrl;
    }

    public string? MetaTitle { get; }

    public string? MetaDescription { get; }

    public string? CanonicalUrl { get; }

    public static SeoMetadata Empty { get; } = new(null, null, null);

    public static SeoMetadata Create(string? metaTitle, string? metaDescription, string? canonicalUrl = null) =>
        new(metaTitle, metaDescription, canonicalUrl);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MetaTitle;
        yield return MetaDescription;
        yield return CanonicalUrl;
    }
}
