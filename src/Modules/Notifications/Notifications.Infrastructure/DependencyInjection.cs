using EventBus;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Abstractions;
using Notifications.Application.OrderConfirmation;
using Notifications.Application.PaymentReceipt;
using Notifications.Application.SendEmail;
using Notifications.Contracts;
using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Repositories;
using Ordering.Contracts;
using Payments.Contracts;
using Persistence.Interceptors;

namespace Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<NotificationsDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"));
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<INotificationSender, FakeEmailSender>();
        services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
        services.AddScoped<INotificationsUnitOfWork, NotificationsUnitOfWork>();

        // Reacts to other modules' integration events (docs/events.md) — dispatched by
        // EventBus.InProcessEventBus, resolved from DI, never called directly.
        services.AddScoped<IIntegrationEventHandler<OrderPlacedIntegrationEvent>, OrderPlacedNotificationHandler>();
        services.AddScoped<IIntegrationEventHandler<PaymentSucceededIntegrationEvent>, PaymentSucceededNotificationHandler>();

        // Dispatchable (ADR-014) counterpart — see SendEmailCommand's doc comment.
        services.AddScoped<IRequestHandler<SendEmailCommand, Unit>, SendEmailCommandHandler>();

        return services;
    }
}
