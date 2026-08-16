using EventBus;
using Ordering.Domain;

namespace Ordering.Application.Abstractions;

public interface ICartRepository
{
    Task AddAsync(Cart cart, CancellationToken cancellationToken = default);

    Task<Cart?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Cart?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<Cart?> GetByAnonymousIdAsync(Guid anonymousId, CancellationToken cancellationToken = default);
}

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IOrderingUnitOfWork
{
    /// <summary>Stages an integration event in the same transaction as whatever change caused it
    /// (the transactional Outbox — see docs/events.md). Does not save by itself; call before
    /// <see cref="SaveChangesAsync"/>.</summary>
    void EnqueueIntegrationEvent(IIntegrationEvent integrationEvent);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
