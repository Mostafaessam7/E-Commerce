using EventBus;
using Payments.Application.Abstractions;
using Payments.Infrastructure.Persistence;

namespace Payments.Infrastructure.Repositories;

internal sealed class PaymentsUnitOfWork : IPaymentsUnitOfWork
{
    private readonly PaymentsDbContext _db;

    public PaymentsUnitOfWork(PaymentsDbContext db) => _db = db;

    public void EnqueueIntegrationEvent(IIntegrationEvent integrationEvent) => _db.EnqueueIntegrationEvent(integrationEvent);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
