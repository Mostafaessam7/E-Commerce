using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Interceptors;
using Promotions.Application.Abstractions;
using Promotions.Application.Coupons;
using Promotions.Contracts;
using Promotions.Infrastructure.Persistence;
using Promotions.Infrastructure.Repositories;

namespace Promotions.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPromotionsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<PromotionsDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"));
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<IPromotionsUnitOfWork, PromotionsUnitOfWork>();
        services.AddScoped<IPromotionsQueries, PromotionsQueries>();

        // Dispatchable (ADR-014) — Ordering's checkout re-validates a cart's coupon code here.
        services.AddScoped<IRequestHandler<RedeemCouponCommand, decimal>, RedeemCouponCommandHandler>();
        services.AddScoped<IRequestHandler<ReleaseCouponCommand, Unit>, ReleaseCouponCommandHandler>();

        // Admin
        services.AddScoped<IRequestHandler<CreateCouponCommand, Guid>, CreateCouponCommandHandler>();
        services.AddScoped<IRequestHandler<ActivateCouponCommand, Unit>, ActivateCouponCommandHandler>();
        services.AddScoped<IRequestHandler<DeactivateCouponCommand, Unit>, DeactivateCouponCommandHandler>();
        services.AddScoped<IRequestHandler<ListCouponsQuery, IReadOnlyList<CouponDto>>, ListCouponsQueryHandler>();

        return services;
    }
}
