using EventBus;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Abstractions;
using Ordering.Domain;
using Ordering.Infrastructure.Persistence;

namespace Ordering.Infrastructure.Repositories;

internal sealed class OrderRepository : IOrderRepository
{
    private readonly OrderingDbContext _db;

    public OrderRepository(OrderingDbContext db) => _db = db;

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default) =>
        await _db.Orders.AddAsync(order, cancellationToken);

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
}

internal sealed class OrderingUnitOfWork : IOrderingUnitOfWork
{
    private readonly OrderingDbContext _db;

    public OrderingUnitOfWork(OrderingDbContext db) => _db = db;

    public void EnqueueIntegrationEvent(IIntegrationEvent integrationEvent) => _db.EnqueueIntegrationEvent(integrationEvent);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
