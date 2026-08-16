using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
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
}
