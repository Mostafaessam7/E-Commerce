using Inventory.Application.Stock;
using Inventory.Contracts;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Repositories;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Interceptors;

namespace Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<InventoryDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"));
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<IStockItemRepository, StockItemRepository>();
        services.AddScoped<IInventoryUnitOfWork, InventoryUnitOfWork>();

        services.AddScoped<IRequestHandler<ReserveStockCommand, Unit>, ReserveStockCommandHandler>();
        services.AddScoped<IRequestHandler<ReleaseStockCommand, Unit>, ReleaseStockCommandHandler>();
        services.AddScoped<IRequestHandler<GetStockQuery, StockLevelDto>, GetStockQueryHandler>();

        return services;
    }
}
