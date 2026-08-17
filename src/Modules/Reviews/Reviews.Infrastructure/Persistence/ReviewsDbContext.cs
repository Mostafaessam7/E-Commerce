using Microsoft.EntityFrameworkCore;
using Persistence;
using Reviews.Domain;

namespace Reviews.Infrastructure.Persistence;

public sealed class ReviewsDbContext : AppDbContextBase
{
    public ReviewsDbContext(DbContextOptions<ReviewsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Review> Reviews => Set<Review>();

    protected override string SchemaName => "reviews";
}
