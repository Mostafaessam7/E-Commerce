using EventBus;
using Microsoft.EntityFrameworkCore;
using Ordering.Domain;
using Persistence;

namespace Ordering.Infrastructure.Persistence;

public sealed class OrderingDbContext : AppDbContextBase
{
    public OrderingDbContext(DbContextOptions<OrderingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<Order> Orders => Set<Order>();

    protected override string SchemaName => "ordering";

    /// <summary>Public wrapper so <c>OrderingUnitOfWork</c> (Repositories/) can reach the
    /// protected <c>EnqueueOutboxMessage</c> on behalf of Application code, which never touches
    /// this DbContext directly.</summary>
    public void EnqueueIntegrationEvent(IIntegrationEvent integrationEvent) => EnqueueOutboxMessage(integrationEvent);
}
