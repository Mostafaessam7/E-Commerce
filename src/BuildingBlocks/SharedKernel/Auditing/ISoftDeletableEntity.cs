namespace SharedKernel.Auditing;

/// <summary>
/// Implemented by entities that are soft-deleted instead of physically removed (e.g. a
/// discontinued Product that historical Orders still reference). The EF Core global query
/// filter added in Phase 2 excludes <see cref="IsDeleted"/> rows from normal queries automatically.
/// Business code should still delete through a domain method (e.g. <c>product.Discontinue()</c>),
/// not by flipping this flag directly — the interface only describes the resulting shape.
/// </summary>
public interface ISoftDeletableEntity
{
    bool IsDeleted { get; }

    DateTime? DeletedAtUtc { get; }

    string? DeletedBy { get; }
}
