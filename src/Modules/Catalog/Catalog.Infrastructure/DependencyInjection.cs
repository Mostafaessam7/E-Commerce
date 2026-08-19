using Catalog.Application.Brands;
using Catalog.Application.Categories;
using Catalog.Application.Products;
using Catalog.Contracts;
using Catalog.Infrastructure.Caching;
using Catalog.Infrastructure.Persistence;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
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

        // Fallback-only registration (TryAdd, see AddDistributedMemoryCache's implementation) —
        // if the composition root already called Caching.DistributedCachingExtensions
        // .AddDistributedCaching (Store.Web does, wiring real Redis when configured), this is a
        // no-op and that registration wins. Standalone compositions (tests, Store.Worker if it
        // ever added this module) still get a working, just not shared, cache instead of a DI
        // resolution failure.
        services.AddDistributedMemoryCache();
        services.AddScoped<Repositories.ProductQueries>();
        services.AddScoped<IProductQueries>(sp =>
            new CachedProductQueries(sp.GetRequiredService<Repositories.ProductQueries>(), sp.GetRequiredService<IDistributedCache>()));

        services.AddScoped<IRequestHandler<CreateProductCommand, Guid>, CreateProductCommandHandler>();
        services.AddScoped<IRequestHandler<GetProductBySlugQuery, ProductDetailsDto>, GetProductBySlugQueryHandler>();
        services.AddScoped<IRequestHandler<SearchProductsQuery, ProductSearchResultDto>, SearchProductsQueryHandler>();
        services.AddScoped<IRequestHandler<GetProductVariantSnapshotQuery, ProductVariantSnapshotDto>, GetProductVariantSnapshotQueryHandler>();

        // Admin (Phase 11)
        services.AddScoped<IRequestHandler<GetProductByIdQuery, ProductDetailsDto>, GetProductByIdQueryHandler>();
        services.AddScoped<IRequestHandler<UpdateProductCommand, Unit>, UpdateProductCommandHandler>();
        services.AddScoped<IRequestHandler<AddProductVariantCommand, Guid>, AddProductVariantCommandHandler>();
        services.AddScoped<IRequestHandler<PublishProductCommand, Unit>, PublishProductCommandHandler>();
        services.AddScoped<IRequestHandler<ArchiveProductCommand, Unit>, ArchiveProductCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteProductCommand, Unit>, DeleteProductCommandHandler>();

        // Brands / Categories (Phase 21 — admin management UI)
        services.AddScoped<IBrandRepository, Repositories.BrandRepository>();
        services.AddScoped<IBrandQueries, Repositories.BrandQueries>();
        services.AddScoped<ICategoryRepository, Repositories.CategoryRepository>();
        services.AddScoped<ICategoryQueries, Repositories.CategoryQueries>();

        services.AddScoped<IRequestHandler<CreateBrandCommand, Guid>, CreateBrandCommandHandler>();
        services.AddScoped<IRequestHandler<ActivateBrandCommand, Unit>, ActivateBrandCommandHandler>();
        services.AddScoped<IRequestHandler<DeactivateBrandCommand, Unit>, DeactivateBrandCommandHandler>();
        services.AddScoped<IRequestHandler<ListBrandsQuery, IReadOnlyList<BrandDto>>, ListBrandsQueryHandler>();

        services.AddScoped<IRequestHandler<CreateCategoryCommand, Guid>, CreateCategoryCommandHandler>();
        services.AddScoped<IRequestHandler<ActivateCategoryCommand, Unit>, ActivateCategoryCommandHandler>();
        services.AddScoped<IRequestHandler<DeactivateCategoryCommand, Unit>, DeactivateCategoryCommandHandler>();
        services.AddScoped<IRequestHandler<ListCategoriesQuery, IReadOnlyList<CategoryDto>>, ListCategoriesQueryHandler>();

        return services;
    }
}
