using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Interceptors;
using Shipping.Application.Abstractions;
using Shipping.Application.Methods;
using Shipping.Contracts;
using Shipping.Infrastructure.Persistence;
using Shipping.Infrastructure.Repositories;

namespace Shipping.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddShippingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<ShippingDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"));
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<IShippingMethodRepository, ShippingMethodRepository>();
        services.AddScoped<IShippingUnitOfWork, ShippingUnitOfWork>();
        services.AddScoped<IShippingQueries, ShippingQueries>();

        // Dispatchable (ADR-014) — checkout lists methods and re-validates the chosen one's cost.
        services.AddScoped<IRequestHandler<ListShippingMethodsQuery, IReadOnlyList<ShippingMethodDto>>, ListShippingMethodsQueryHandler>();
        services.AddScoped<IRequestHandler<GetShippingMethodQuery, ShippingMethodDto>, GetShippingMethodQueryHandler>();

        // Admin
        services.AddScoped<IRequestHandler<CreateShippingMethodCommand, Guid>, CreateShippingMethodCommandHandler>();
        services.AddScoped<IRequestHandler<ActivateShippingMethodCommand, Unit>, ActivateShippingMethodCommandHandler>();
        services.AddScoped<IRequestHandler<DeactivateShippingMethodCommand, Unit>, DeactivateShippingMethodCommandHandler>();

        return services;
    }
}
