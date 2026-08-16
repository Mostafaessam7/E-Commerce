using System.Text.Json;
using EventBus;
using Microsoft.EntityFrameworkCore;
using Persistence.Outbox;

namespace Persistence;

/// <summary>
/// Base for every module's DbContext (one per module — ADR-005). Gives every module the same
/// OutboxMessages table/config and the same soft-delete query-filter convention for free;
/// module-specific entity configurations are picked up automatically via
/// <c>ApplyConfigurationsFromAssembly</c> against the *derived* context's own assembly. Auditing
/// is wired separately via <c>AuditingInterceptor</c> (registered in DbContextOptions, not
/// inheritance) so it also applies to contexts that can't derive from this base — namely
/// <c>AppIdentityDbContext</c>, which must derive from ASP.NET Core Identity's
/// <c>IdentityDbContext</c> instead.
/// </summary>
public abstract class AppDbContextBase : DbContext
{
    protected AppDbContextBase(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplySoftDeleteQueryFilter();
    }

    /// <summary>
    /// Adds an outbox row for <paramref name="integrationEvent"/> to this context's own pending
    /// change set — call it from the same unit of work that changes the aggregate, before
    /// <c>SaveChangesAsync</c>, so both commit in one transaction. Does not save by itself.
    /// </summary>
    protected void EnqueueOutboxMessage(IIntegrationEvent integrationEvent)
    {
        var eventType = integrationEvent.GetType();

        var message = new OutboxMessage(
            id: integrationEvent.EventId,
            type: eventType.FullName ?? eventType.Name,
            content: JsonSerializer.Serialize(integrationEvent, eventType),
            occurredOnUtc: integrationEvent.OccurredOnUtc);

        OutboxMessages.Add(message);
    }
}
