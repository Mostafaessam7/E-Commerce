using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Persistence.Interceptors;

namespace IntegrationTests.Persistence;

public sealed class AppDbContextBaseTests
{
    private static TestDbContext CreateContext(FakeDateTimeProvider dateTimeProvider, FakeCurrentUser currentUser)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditingInterceptor(dateTimeProvider, currentUser))
            .Options;

        return new TestDbContext(options);
    }

    [Fact]
    public async Task Adding_an_entity_stamps_CreatedAtUtc_and_CreatedBy()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var dateTimeProvider = new FakeDateTimeProvider(now);
        var currentUser = new FakeCurrentUser { IsAuthenticated = true, Email = "admin@example.com" };
        await using var context = CreateContext(dateTimeProvider, currentUser);

        var aggregate = new TestAggregate(Guid.NewGuid(), "first");
        context.Aggregates.Add(aggregate);
        await context.SaveChangesAsync();

        aggregate.CreatedAtUtc.Should().Be(now);
        aggregate.CreatedBy.Should().Be("admin@example.com");
        aggregate.LastModifiedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Modifying_an_entity_stamps_LastModifiedAtUtc_and_LastModifiedBy()
    {
        var dateTimeProvider = new FakeDateTimeProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var currentUser = new FakeCurrentUser { IsAuthenticated = true, Email = "admin@example.com" };
        await using var context = CreateContext(dateTimeProvider, currentUser);

        var aggregate = new TestAggregate(Guid.NewGuid(), "first");
        context.Aggregates.Add(aggregate);
        await context.SaveChangesAsync();

        var modifiedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        dateTimeProvider.UtcNow = modifiedAt;
        currentUser.Email = "editor@example.com";
        aggregate.Rename("second");
        await context.SaveChangesAsync();

        aggregate.LastModifiedAtUtc.Should().Be(modifiedAt);
        aggregate.LastModifiedBy.Should().Be("editor@example.com");
    }

    [Fact]
    public async Task Unauthenticated_changes_are_attributed_to_system()
    {
        await using var context = CreateContext(new FakeDateTimeProvider(DateTime.UtcNow), new FakeCurrentUser());

        var aggregate = new TestAggregate(Guid.NewGuid(), "first");
        context.Aggregates.Add(aggregate);
        await context.SaveChangesAsync();

        aggregate.CreatedBy.Should().Be("system");
    }

    [Fact]
    public async Task Soft_deleted_entities_are_excluded_from_normal_queries()
    {
        var dateTimeProvider = new FakeDateTimeProvider(DateTime.UtcNow);
        var currentUser = new FakeCurrentUser();
        await using var context = CreateContext(dateTimeProvider, currentUser);

        var aggregate = new TestAggregate(Guid.NewGuid(), "first");
        context.Aggregates.Add(aggregate);
        await context.SaveChangesAsync();

        aggregate.Delete(dateTimeProvider.UtcNow, "admin@example.com");
        await context.SaveChangesAsync();

        var visible = await context.Aggregates.ToListAsync();

        visible.Should().BeEmpty();
    }

    [Fact]
    public async Task Enqueuing_an_outbox_message_persists_it_in_the_same_SaveChanges_call()
    {
        await using var context = CreateContext(new FakeDateTimeProvider(DateTime.UtcNow), new FakeCurrentUser());

        context.PublishForTest(new TestIntegrationEvent("hello"));
        await context.SaveChangesAsync();

        var stored = await context.OutboxMessages.SingleAsync();

        stored.Type.Should().Contain(nameof(TestIntegrationEvent));
        stored.Content.Should().Contain("hello");
        stored.ProcessedOnUtc.Should().BeNull();
    }
}
