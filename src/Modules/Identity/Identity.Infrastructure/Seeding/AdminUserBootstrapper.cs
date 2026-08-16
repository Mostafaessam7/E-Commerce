using Identity.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Seeding;

/// <summary>
/// Opt-in, dev-only convenience: if <c>Identity:DefaultAdmin:Email</c>/<c>Password</c> are
/// configured (User Secrets locally — never <c>appsettings.json</c>, these are real login
/// credentials, unlike Payments' fake webhook secret), creates that user pre-confirmed and in the
/// "Admin" role the first time the app starts, so the Admin panel (Phase 11) has someone who can
/// log into it without a manual DB edit. Does nothing if either setting is absent — the app never
/// depends on this running, exactly like <see cref="PermissionRoleSeeder"/> not creating a
/// default admin was a deliberate choice (see that class's doc comment); this just gives the
/// *option* back for local development.
/// </summary>
public sealed class AdminUserBootstrapper : IHostedService
{
    private const string AdminRoleName = "Admin";

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminUserBootstrapper> _logger;

    public AdminUserBootstrapper(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<AdminUserBootstrapper> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = _configuration["Identity:DefaultAdmin:Email"];
        var password = _configuration["Identity:DefaultAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        try
        {
            await SeedAsync(email, password);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipped default admin bootstrap — database may not be migrated yet.");
        }
    }

    private async Task SeedAsync(string email, string password)
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return;
        }

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Could not create default admin user: {Errors}", string.Join(" ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, AdminRoleName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
