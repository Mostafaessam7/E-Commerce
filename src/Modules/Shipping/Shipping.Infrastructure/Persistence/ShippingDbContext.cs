using Microsoft.EntityFrameworkCore;
using Persistence;
using Shipping.Domain;

namespace Shipping.Infrastructure.Persistence;

public sealed class ShippingDbContext : AppDbContextBase
{
    public ShippingDbContext(DbContextOptions<ShippingDbContext> options)
        : base(options)
    {
    }

    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();

    protected override string SchemaName => "shipping";
}
