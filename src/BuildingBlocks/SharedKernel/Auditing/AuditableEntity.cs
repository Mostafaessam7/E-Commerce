using SharedKernel.Primitives;

namespace SharedKernel.Auditing;

/// <summary>
/// Opt-in base for entities that want created/modified auditing without hand-writing the four
/// properties every time. The setters are private on purpose: EF Core's change tracker writes to
/// them through its own metadata-based property access (not the public C# setter), so nothing
/// outside the auditing interceptor can mutate them. Inherit this instead of
/// <see cref="Entity{TId}"/> directly when a module needs auditing on that entity; it's a plain
/// interface otherwise, so nothing forces every entity through this base.
/// </summary>
public abstract class AuditableEntity<TId> : Entity<TId>, IAuditableEntity
    where TId : notnull
{
    protected AuditableEntity(TId id)
        : base(id)
    {
    }

    protected AuditableEntity()
    {
    }

    public DateTime CreatedAtUtc { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTime? LastModifiedAtUtc { get; private set; }

    public string? LastModifiedBy { get; private set; }
}
