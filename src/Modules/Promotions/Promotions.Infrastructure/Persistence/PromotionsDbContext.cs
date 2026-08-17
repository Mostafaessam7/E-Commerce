using Microsoft.EntityFrameworkCore;
using Persistence;
using Promotions.Domain;

namespace Promotions.Infrastructure.Persistence;

public sealed class PromotionsDbContext : AppDbContextBase
{
    public PromotionsDbContext(DbContextOptions<PromotionsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Coupon> Coupons => Set<Coupon>();

    protected override string SchemaName => "promotions";
}
