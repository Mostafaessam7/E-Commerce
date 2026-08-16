using Identity.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Security;

namespace Identity.Infrastructure.Seeding;

/// <summary>
/// Ensures an "Admin" role exists holding every permission claim from
/// <see cref="Permissions.All"/> — safe to run on every startup (idempotent, adds only what's
/// missing). Deliberately does NOT create a default admin user/password: seeding credentials
/// belongs to a deployment-time step (config-driven, out of source control), not application
/// startup code.
/// </summary>
public sealed class PermissionRoleSeeder : IHostedService
{
    private const string AdminRoleName = "Admin";

    private readonly IServiceProvider _serviceProvider;

    public PermissionRoleSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        var adminRole = await roleManager.FindByNameAsync(AdminRoleName);
        if (adminRole is null)
        {
            adminRole = new ApplicationRole(AdminRoleName);
            await roleManager.CreateAsync(adminRole);
        }

        var existingClaims = (await roleManager.GetClaimsAsync(adminRole))
            .Where(c => c.Type == CustomClaimTypes.Permission)
            .Select(c => c.Value)
            .ToHashSet();

        foreach (var permission in Permissions.All.Where(p => !existingClaims.Contains(p)))
        {
            await roleManager.AddClaimAsync(adminRole, new System.Security.Claims.Claim(CustomClaimTypes.Permission, permission));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
