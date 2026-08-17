using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Promotions.Infrastructure.Persistence;

public sealed class PromotionsDbContextFactory : IDesignTimeDbContextFactory<PromotionsDbContext>
{
    public PromotionsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PromotionsDbContext>();

        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True");

        return new PromotionsDbContext(optionsBuilder.Options);
    }
}
