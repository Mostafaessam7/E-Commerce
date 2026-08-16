using SharedKernel.Primitives;

namespace Catalog.Domain;

public sealed class ProductImage : Entity<Guid>
{
    internal ProductImage(Guid id, string url, string? altText, int displayOrder, bool isPrimary)
        : base(id)
    {
        Url = url;
        AltText = altText;
        DisplayOrder = displayOrder;
        IsPrimary = isPrimary;
    }

    private ProductImage()
    {
    }

    public string Url { get; private set; } = null!;

    public string? AltText { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsPrimary { get; private set; }

    internal void MakePrimary() => IsPrimary = true;

    internal void MakeSecondary() => IsPrimary = false;
}
