using Catalog.Domain.ValueObjects;
using SharedKernel.Auditing;
using SharedKernel.Exceptions;
using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;

namespace Catalog.Domain;

/// <summary>Nested via adjacency list (<see cref="ParentId"/>) — simplest model that satisfies
/// Section 4's "nested categories" requirement; upgrade to a materialized path only if deep
/// category-tree queries become a measured performance problem.</summary>
public sealed class Category : AggregateRoot<Guid>, ISoftDeletableEntity
{
    private Category(Guid id, string name, Slug slug, Guid? parentId)
        : base(id)
    {
        Name = name;
        Slug = slug;
        ParentId = parentId;
    }

    private Category()
    {
    }

    public string Name { get; private set; } = null!;

    public Slug Slug { get; private set; } = null!;

    public Guid? ParentId { get; private set; }

    public string? Description { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public static Result<Category> Create(string name, string slug, Guid? parentId = null)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));

        var slugResult = Slug.FromText(slug);
        if (slugResult.IsFailure)
        {
            return Result.Failure<Category>(slugResult.Error);
        }

        return Result.Success(new Category(Guid.NewGuid(), name, slugResult.Value, parentId));
    }

    public void Rename(string name) => Name = Guard.Against.NullOrWhiteSpace(name, nameof(name));

    public void MoveTo(Guid? parentId)
    {
        if (parentId == Id)
        {
            throw new DomainException("A category cannot be its own parent.");
        }

        ParentId = parentId;
    }

    public void SetDescription(string? description) => Description = description;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void Delete(DateTime deletedAtUtc, string deletedBy)
    {
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
        DeletedBy = deletedBy;
    }
}
