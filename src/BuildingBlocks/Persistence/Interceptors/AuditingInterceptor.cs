using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Security;
using SharedKernel.Auditing;

namespace Persistence.Interceptors;

/// <summary>
/// Stamps <see cref="IAuditableEntity"/> fields on every <c>SaveChanges</c> call, for any module's
/// DbContext that registers it (see <see cref="AppDbContextBase"/>). Uses EF's property-metadata
/// accessor (<c>entry.Property(...).CurrentValue</c>), not the C# setter, so
/// <see cref="SharedKernel.Auditing.AuditableEntity{TId}"/>'s private setters stay private to
/// everything except this interceptor.
/// </summary>
public sealed class AuditingInterceptor : SaveChangesInterceptor
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUser _currentUser;

    public AuditingInterceptor(IDateTimeProvider dateTimeProvider, ICurrentUser currentUser)
    {
        _dateTimeProvider = dateTimeProvider;
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditInfo(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _dateTimeProvider.UtcNow;
        var actor = _currentUser.IsAuthenticated
            ? _currentUser.Email ?? _currentUser.UserId?.ToString() ?? "unknown"
            : "system";

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).CurrentValue = now;
                entry.Property(nameof(IAuditableEntity.CreatedBy)).CurrentValue = actor;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditableEntity.LastModifiedAtUtc)).CurrentValue = now;
                entry.Property(nameof(IAuditableEntity.LastModifiedBy)).CurrentValue = actor;
            }
        }
    }
}
