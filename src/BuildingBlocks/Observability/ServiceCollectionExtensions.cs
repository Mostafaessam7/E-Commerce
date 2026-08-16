using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Observability;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddObservabilityCore(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationIdProvider, HttpContextCorrelationIdProvider>();

        return services;
    }
}
