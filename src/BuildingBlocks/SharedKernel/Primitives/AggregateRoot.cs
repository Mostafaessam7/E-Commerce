using SharedKernel.Auditing;

namespace SharedKernel.Primitives;

/// <summary>
/// Marks an <see cref="Entity{TId}"/> as an aggregate root — the only kind of object a module's
/// Application layer is allowed to load, mutate, and persist directly. Everything reachable
/// only through the aggregate root (order items, product variants, ...) is loaded and saved as
/// part of it; other modules never reference those child entities directly, only the root
/// (and only through that module's Contracts, never the concrete class).
///
/// Derives from <see cref="AuditableEntity{TId}"/> rather than <see cref="Entity{TId}"/> directly
/// because virtually every aggregate root in this system wants created/modified tracking —
/// opting in per-aggregate turned out to be pure boilerplate once real modules (Catalog) started
/// using it. Child entities that don't need auditing keep deriving from <see cref="Entity{TId}"/>
/// directly. Soft-delete (<see cref="ISoftDeletableEntity"/>) stays opt-in per aggregate — not
/// every aggregate root should be soft-deletable (e.g. Payments records never are).
///
/// Concurrency note: optimistic concurrency tokens (EF Core's <c>[Timestamp]</c> / rowversion,
/// or a shadow property configured with <c>IsRowVersion()</c>) are a persistence concern, not a
/// domain one — they are configured on the EF entity mapping in each module's Infrastructure
/// project, not modeled as a property here. Keeping it out of the domain model avoids leaking a
/// storage detail into business logic while still giving Inventory (and any other module with
/// concurrent-write hot paths) real protection against lost updates.
/// </summary>
public abstract class AggregateRoot<TId> : AuditableEntity<TId>
    where TId : notnull
{
    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    protected AggregateRoot()
    {
    }
}
