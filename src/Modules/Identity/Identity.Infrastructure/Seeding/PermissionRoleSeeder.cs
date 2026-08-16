using Identity.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Security;

namespace Identity.Infrastructure.Seeding;

/// <summary>
/// Ensures an "Admin" role exists holding every permission claim from
/// <see cref="Permissions.All"/> — safe to run on every startup (idempotent, adds only what's
/// missing). Deliberately does NOT create a default admin user/password: seeding credentials
/// belongs to a deployment-time step (config-driven, out of source control), not application
/// startup code.
///
/// Failures here are logged and swallowed, not rethrown: this runs as an <see cref="IHostedService"/>
/// during host startup, where an unhandled exception aborts the ENTIRE application — including the
/// storefront pages that have nothing to do with permissions. A database that isn't reachable or
/// hasn't been migrated yet (a fresh clone before `dotnet ef database update`) shouldn't take the
/// whole site down; it should just mean seeding didn't happen yet.
/// </summary>
public sealed class PermissionRoleSeeder : IHostedService
{
    private const string AdminRoleName = "Admin";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PermissionRoleSeeder> _logger;

    public PermissionRoleSeeder(IServiceProvider serviceProvider, ILogger<PermissionRoleSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipped permission role seeding — database may not be migrated yet.");
        }
    }

    private async Task SeedAsync()
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
