using Microsoft.EntityFrameworkCore;
using Shipping.Application.Abstractions;
using Shipping.Domain;
using Shipping.Infrastructure.Persistence;

namespace Shipping.Infrastructure.Repositories;

internal sealed class ShippingMethodRepository : IShippingMethodRepository
{
    private readonly ShippingDbContext _db;

    public ShippingMethodRepository(ShippingDbContext db) => _db = db;

    public async Task AddAsync(ShippingMethod method, CancellationToken cancellationToken = default) =>
        await _db.ShippingMethods.AddAsync(method, cancellationToken);

    public Task<ShippingMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.ShippingMethods.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
}

internal sealed class ShippingUnitOfWork : IShippingUnitOfWork
{
    private readonly ShippingDbContext _db;

    public ShippingUnitOfWork(ShippingDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
