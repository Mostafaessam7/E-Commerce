using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Caching;

/// <summary>
/// Registers <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> — the
/// Redis container docker-compose.yml has provisioned since Phase 13 gets its first real reader
/// here (Catalog's storefront product queries, see <c>Catalog.Infrastructure.Caching</c>).
/// </summary>
public static class DistributedCachingExtensions
{
    /// <summary>
    /// Real Redis when <c>ConnectionStrings:Redis</c> is configured (docker-compose.yml sets it);
    /// an in-process fallback otherwise — same "the app never depends on this running" posture as
    /// <c>ApplyMigrationsOnStartup</c> and <c>AdminUserBootstrapper</c> (docs/deployment.md,
    /// docs/security.md): a developer running `dotnet run` against LocalDB with no Redis container
    /// up still gets a working — just not shared/persistent — cache, not a startup failure.
    /// </summary>
    public static IServiceCollection AddDistributedCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "ecommerce:";
            });
        }

        return services;
    }
}
