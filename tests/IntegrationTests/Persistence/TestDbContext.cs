using EventBus;
using Microsoft.EntityFrameworkCore;
using Persistence;
using SharedKernel.Auditing;

namespace IntegrationTests.Persistence;

/// <summary>Minimal DbContext exercising AppDbContextBase — auditing, outbox, soft-delete filter.</summary>
public sealed class TestAggregate : AuditableEntity<Guid>, ISoftDeletableEntity
{
    private TestAggregate()
    {
    }

    public TestAggregate(Guid id, string name)
        : base(id)
    {
        Name = name;
    }

    public string Name { get; private set; } = null!;

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public void Rename(string name) => Name = name;

    public void Delete(DateTime deletedAtUtc, string deletedBy)
    {
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
        DeletedBy = deletedBy;
    }
}

public sealed record TestIntegrationEvent(string Payload) : IntegrationEvent;

public sealed class TestDbContext : AppDbContextBase
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();

    protected override string SchemaName => "test";

    public void PublishForTest(IIntegrationEvent integrationEvent) => EnqueueOutboxMessage(integrationEvent);
}
