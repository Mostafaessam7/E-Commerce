using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Security;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSecurityCore(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        return services;
    }
}
