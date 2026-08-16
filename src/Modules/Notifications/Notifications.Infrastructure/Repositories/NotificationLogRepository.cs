using Notifications.Application.Abstractions;
using Notifications.Domain;
using Notifications.Infrastructure.Persistence;

namespace Notifications.Infrastructure.Repositories;

internal sealed class NotificationLogRepository : INotificationLogRepository
{
    private readonly NotificationsDbContext _db;

    public NotificationLogRepository(NotificationsDbContext db) => _db = db;

    public async Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default) =>
        await _db.NotificationLogs.AddAsync(log, cancellationToken);
}

internal sealed class NotificationsUnitOfWork : INotificationsUnitOfWork
{
    private readonly NotificationsDbContext _db;

    public NotificationsUnitOfWork(NotificationsDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
