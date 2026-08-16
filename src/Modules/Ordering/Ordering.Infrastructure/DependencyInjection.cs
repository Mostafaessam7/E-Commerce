using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Abstractions;
using Ordering.Application.Carts;
using Ordering.Application.Checkout;
using Ordering.Contracts;
using Ordering.Infrastructure.Persistence;
using Ordering.Infrastructure.Repositories;
using Persistence.Interceptors;

namespace Ordering.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<OrderingDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"));
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderingUnitOfWork, OrderingUnitOfWork>();

        services.AddScoped<IRequestHandler<GetOrCreateCartCommand, CartDto>, GetOrCreateCartCommandHandler>();
        services.AddScoped<IRequestHandler<AddCartItemCommand, CartDto>, AddCartItemCommandHandler>();
        services.AddScoped<IRequestHandler<RemoveCartItemCommand, CartDto>, RemoveCartItemCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateCartItemQuantityCommand, CartDto>, UpdateCartItemQuantityCommandHandler>();
        services.AddScoped<IRequestHandler<ApplyCouponCommand, CartDto>, ApplyCouponCommandHandler>();
        services.AddScoped<IRequestHandler<RemoveCouponCommand, CartDto>, RemoveCouponCommandHandler>();
        services.AddScoped<IRequestHandler<MergeCartCommand, CartDto>, MergeCartCommandHandler>();
        services.AddScoped<IRequestHandler<GetCartQuery, CartDto>, GetCartQueryHandler>();

        services.AddScoped<IRequestHandler<PlaceOrderCommand, Guid>, PlaceOrderCommandHandler>();
        services.AddScoped<IRequestHandler<GetOrderQuery, OrderDto>, GetOrderQueryHandler>();
        services.AddScoped<IRequestHandler<MarkOrderAsPaidCommand, Unit>, MarkOrderAsPaidCommandHandler>();

        return services;
    }
}
