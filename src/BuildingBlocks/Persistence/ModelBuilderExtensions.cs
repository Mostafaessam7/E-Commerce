using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SharedKernel.Auditing;

namespace Persistence;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies <c>HasQueryFilter(e => !e.IsDeleted)</c> to every entity type implementing
    /// <see cref="ISoftDeletableEntity"/> in the model, so soft-deleted rows are excluded from
    /// normal queries automatically (see <c>ISoftDeletableEntity</c>'s doc comment — business
    /// code still deletes through a domain method, this only hides the result from reads).
    /// </summary>
    public static void ApplySoftDeleteQueryFilter(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletableEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletableEntity.IsDeleted));
            var notDeleted = Expression.Equal(isDeletedProperty, Expression.Constant(false));
            var lambda = Expression.Lambda(notDeleted, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }

    /// <summary>
    /// Every entity in this codebase assigns its own <c>Guid.NewGuid()</c> in its constructor —
    /// nothing is database-generated. Left at EF Core's default (<c>ValueGeneratedOnAdd</c> for
    /// Guid keys by convention), this produces a real, hard-to-spot bug: when a *new* child
    /// entity is added to an already-tracked parent's collection (not created together with the
    /// parent in the same call that added the parent — e.g. attaching a new domain event/child
    /// row to an aggregate that was loaded, not just constructed), EF Core's heuristic for
    /// deciding Added-vs-Unchanged sees a non-default key value on a never-before-seen entity and
    /// assumes it already exists, generating an UPDATE instead of an INSERT — which then fails
    /// with "0 rows affected" because that row was never there. Explicitly marking every
    /// domain-assigned Guid key <c>ValueGeneratedNever()</c> removes the ambiguity: EF stops
    /// guessing and treats every not-yet-tracked entity as Added, which is always correct here.
    /// Skips owned types (e.g. <c>ProductVariantOptions</c>' shadow Id), which explicitly opt
    /// into <c>ValueGeneratedOnAdd()</c> for a real surrogate key EF itself generates.
    /// </summary>
    public static void MarkDomainAssignedGuidKeysAsNeverGenerated(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
            {
                continue;
            }

            var idProperty = entityType.FindProperty("Id");
            if (idProperty is not null && idProperty.ClrType == typeof(Guid))
            {
                idProperty.ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}
