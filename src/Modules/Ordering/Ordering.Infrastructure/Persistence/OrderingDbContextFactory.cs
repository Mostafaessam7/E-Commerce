using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ordering.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef` — see CatalogDbContextFactory's doc comment.</summary>
public sealed class OrderingDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrderingDbContext>();

        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True");

        return new OrderingDbContext(optionsBuilder.Options);
    }
}
