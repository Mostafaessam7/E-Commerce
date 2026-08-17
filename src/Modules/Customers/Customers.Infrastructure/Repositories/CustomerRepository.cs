using Customers.Application.Abstractions;
using Customers.Domain;
using Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Customers.Infrastructure.Repositories;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly CustomersDbContext _db;

    public CustomerRepository(CustomersDbContext db) => _db = db;

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default) =>
        await _db.Customers.AddAsync(customer, cancellationToken);

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Customers.Include(c => c.Addresses).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
}

internal sealed class CustomersUnitOfWork : ICustomersUnitOfWork
{
    private readonly CustomersDbContext _db;

    public CustomersUnitOfWork(CustomersDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
