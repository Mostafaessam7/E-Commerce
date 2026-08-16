using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Abstractions;
using Payments.Application.Payments;
using Payments.Infrastructure.Persistence;
using Payments.Infrastructure.Repositories;
using Persistence.Interceptors;

namespace Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<PaymentsDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"));
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<IPaymentGateway, FakePaymentGateway>();
        services.AddScoped<IWebhookSimulator, FakeWebhookSimulator>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();
        services.AddScoped<IPaymentsUnitOfWork, PaymentsUnitOfWork>();

        services.AddScoped<IRequestHandler<InitializePaymentCommand, InitializePaymentResultDto>, InitializePaymentCommandHandler>();
        services.AddScoped<IRequestHandler<ProcessWebhookCommand, Unit>, ProcessWebhookCommandHandler>();
        services.AddScoped<IRequestHandler<RefundPaymentCommand, Unit>, RefundPaymentCommandHandler>();
        services.AddScoped<IRequestHandler<GetPaymentQuery, PaymentDto>, GetPaymentQueryHandler>();

        return services;
    }
}
