using Microsoft.EntityFrameworkCore;
using Notifications.Domain;
using Persistence;

namespace Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext : AppDbContextBase
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override string SchemaName => "notifications";
}
