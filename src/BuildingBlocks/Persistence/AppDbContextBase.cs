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
/// <c>IdentityDbContext</c> instead (and sets its own schema directly — see that class).
///
/// All modules currently share one physical database/connection string (see docs/database.md) —
/// nothing stops two modules mapping a same-named table (every module gets an "OutboxMessages"
/// table from this base alone) into the same default "dbo" schema and colliding. Each module gets
/// its own SQL schema via <see cref="SchemaName"/> so table names only need to be unique within a
/// module, matching "one DbContext per module" being a real logical boundary and not just a C#
/// convenience.
/// </summary>
public abstract class AppDbContextBase : DbContext
{
    protected AppDbContextBase(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>The SQL schema this module's tables live in, e.g. "catalog", "inventory".</summary>
    protected abstract string SchemaName { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplySoftDeleteQueryFilter();
        modelBuilder.MarkDomainAssignedGuidKeysAsNeverGenerated();
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
