using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> create a <see cref="CatalogDbContext"/> without building the whole
/// application host (Program.cs, full DI container, real configuration/secrets) — just enough to
/// generate/apply migrations. Every module's Infrastructure project gets one of these once it has
/// a DbContext.
/// </summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();

        // Only used to generate migration files against a schema shape — never actually
        // connects during `migrations add`. Real connection strings come from Store.Web's
        // configuration at runtime (see docs/database.md).
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True");

        return new CatalogDbContext(optionsBuilder.Options);
    }
}
