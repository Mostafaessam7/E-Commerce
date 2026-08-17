using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Interceptors;
using Reviews.Application.Abstractions;
using Reviews.Application.Reviews;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Repositories;

namespace Reviews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReviewsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<ReviewsDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"));
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IReviewsUnitOfWork, ReviewsUnitOfWork>();
        services.AddScoped<IReviewsQueries, ReviewsQueries>();

        // Storefront
        services.AddScoped<IRequestHandler<SubmitReviewCommand, Guid>, SubmitReviewCommandHandler>();
        services.AddScoped<IRequestHandler<GetProductReviewsQuery, ProductReviewsDto>, GetProductReviewsQueryHandler>();

        // Admin
        services.AddScoped<IRequestHandler<ApproveReviewCommand, Unit>, ApproveReviewCommandHandler>();
        services.AddScoped<IRequestHandler<RejectReviewCommand, Unit>, RejectReviewCommandHandler>();
        services.AddScoped<IRequestHandler<ListReviewsQuery, IReadOnlyList<ReviewDto>>, ListReviewsQueryHandler>();

        return services;
    }
}
