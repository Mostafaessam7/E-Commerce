using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reviews.Infrastructure.Persistence;

public sealed class ReviewsDbContextFactory : IDesignTimeDbContextFactory<ReviewsDbContext>
{
    public ReviewsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReviewsDbContext>();

        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True");

        return new ReviewsDbContext(optionsBuilder.Options);
    }
}
