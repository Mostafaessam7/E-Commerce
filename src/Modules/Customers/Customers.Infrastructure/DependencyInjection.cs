using Customers.Application.Abstractions;
using Customers.Application.Profile;
using Customers.Infrastructure.Persistence;
using Customers.Infrastructure.Repositories;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Interceptors;

namespace Customers.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<CustomersDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"));
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomersUnitOfWork, CustomersUnitOfWork>();

        services.AddScoped<IRequestHandler<GetOrCreateCustomerCommand, CustomerProfileDto>, GetOrCreateCustomerCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateProfileCommand, Unit>, UpdateProfileCommandHandler>();
        services.AddScoped<IRequestHandler<AddAddressCommand, Guid>, AddAddressCommandHandler>();
        services.AddScoped<IRequestHandler<RemoveAddressCommand, Unit>, RemoveAddressCommandHandler>();
        services.AddScoped<IRequestHandler<SetDefaultAddressCommand, Unit>, SetDefaultAddressCommandHandler>();
        services.AddScoped<IRequestHandler<GetCustomerProfileQuery, CustomerProfileDto>, GetCustomerProfileQueryHandler>();

        return services;
    }
}
