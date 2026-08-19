using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EndToEndTests;

/// <summary>
/// Boots the real <c>Store.Web</c> host in-memory (<see cref="WebApplicationFactory{TEntryPoint}"/>)
/// — real MVC pipeline, real Razor rendering, real antiforgery tokens, real cookie auth — against
/// the same shared LocalDB instance <c>IntegrationTests</c> uses (docs/testing.md). Deliberately
/// the real HTTP surface, not the plain <c>ServiceCollection</c> composition IntegrationTests uses:
/// this is what actually proves a browser-driven user journey works end to end, not just that the
/// handlers behind it do.
/// </summary>
public sealed class StoreWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Same LocalDB connection string every other test project hardcodes (docs/testing.md) —
        // the DB is assumed already migrated (docs/database.md), never created by this factory.
        // Redis (ConnectionStrings:Redis) is deliberately left unset — Catalog's cache falls back
        // to AddDistributedMemoryCache (ADR-033), exactly like every other test composition.
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
            [
                new("ConnectionStrings:Database",
                    "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"),
                new("Payments:WebhookSecret", "e2e-test-webhook-secret"),
                new("ApplyMigrationsOnStartup", "false"),
            ]);
        });
    }
}
