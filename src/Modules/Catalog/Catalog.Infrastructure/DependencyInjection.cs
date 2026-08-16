using Catalog.Application.Products;
using Catalog.Infrastructure.Persistence;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Interceptors;

namespace Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"));
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<IProductRepository, Repositories.ProductRepository>();
        services.AddScoped<ICatalogUnitOfWork, Repositories.CatalogUnitOfWork>();
        services.AddScoped<IProductQueries, Repositories.ProductQueries>();

        services.AddScoped<IRequestHandler<CreateProductCommand, Guid>, CreateProductCommandHandler>();
        services.AddScoped<IRequestHandler<GetProductBySlugQuery, ProductDetailsDto>, GetProductBySlugQueryHandler>();
        services.AddScoped<IRequestHandler<SearchProductsQuery, ProductSearchResultDto>, SearchProductsQueryHandler>();

        return services;
    }
}
