namespace SharedKernel.Primitives;

/// <summary>
/// Base type for every domain entity in every module. Identity-based equality only —
/// two entities are equal iff they are the same runtime type and carry the same Id.
/// Never compare entities by their properties; that's what <see cref="ValueObject"/> is for.
/// </summary>
/// <typeparam name="TId">
/// The entity's identifier type. Prefer a strongly-typed id (e.g. <c>ProductId</c>, a readonly
/// record struct wrapping a Guid) over a bare <see cref="Guid"/> once a module's domain grows
/// enough aggregates that swapping two ids by mistake becomes a real risk — nothing in Phase 1
/// forces that choice, each module decides it independently in its own Domain project.
/// </typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>
    /// Reserved for EF Core materialization. EF Core populates <see cref="Id"/> and every other
    /// backing field directly through its own metadata (constructor binding or field access),
    /// bypassing this constructor's "no id yet" state entirely — application code must never
    /// call this and must never observe a domain entity in this half-constructed condition.
    /// </summary>
#pragma warning disable CS8618
    protected Entity()
    {
    }
#pragma warning restore CS8618

    public TId Id { get; protected set; }

    /// <summary>
    /// Domain events raised by this entity since it was loaded/created, not yet dispatched.
    /// Application-layer command handlers read this off the aggregate root after
    /// <c>SaveChangesAsync</c> succeeds and hand each event to the in-process dispatcher.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
