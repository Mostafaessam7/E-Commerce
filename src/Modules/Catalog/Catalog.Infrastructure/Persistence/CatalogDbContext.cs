using Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext : AppDbContextBase
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<ProductAttribute> Attributes => Set<ProductAttribute>();

    protected override string SchemaName => "catalog";
}
