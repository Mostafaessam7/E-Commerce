using Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace Inventory.Infrastructure.Persistence;

public sealed class InventoryDbContext : AppDbContextBase
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<StockItem> StockItems => Set<StockItem>();

    protected override string SchemaName => "inventory";
}
