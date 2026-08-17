using Customers.Domain;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace Customers.Infrastructure.Persistence;

public sealed class CustomersDbContext : AppDbContextBase
{
    public CustomersDbContext(DbContextOptions<CustomersDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    protected override string SchemaName => "customers";
}
