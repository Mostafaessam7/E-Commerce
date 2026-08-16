using Catalog.Domain.ValueObjects;
using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;

namespace Catalog.Domain;

public sealed class Brand : AggregateRoot<Guid>
{
    private Brand(Guid id, string name, Slug slug)
        : base(id)
    {
        Name = name;
        Slug = slug;
    }

    private Brand()
    {
    }

    public string Name { get; private set; } = null!;

    public Slug Slug { get; private set; } = null!;

    public string? LogoUrl { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static Result<Brand> Create(string name, string slug)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));

        var slugResult = Slug.FromText(slug);
        return slugResult.IsFailure
            ? Result.Failure<Brand>(slugResult.Error)
            : Result.Success(new Brand(Guid.NewGuid(), name, slugResult.Value));
    }

    public void Rename(string name) => Name = Guard.Against.NullOrWhiteSpace(name, nameof(name));

    public void SetLogo(string? logoUrl) => LogoUrl = logoUrl;

    public void SetDescription(string? description) => Description = description;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
