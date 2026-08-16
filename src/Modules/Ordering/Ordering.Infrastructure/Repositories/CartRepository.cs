using Microsoft.EntityFrameworkCore;
using Ordering.Application.Abstractions;
using Ordering.Domain;
using Ordering.Infrastructure.Persistence;

namespace Ordering.Infrastructure.Repositories;

internal sealed class CartRepository : ICartRepository
{
    private readonly OrderingDbContext _db;

    public CartRepository(OrderingDbContext db) => _db = db;

    public async Task AddAsync(Cart cart, CancellationToken cancellationToken = default) =>
        await _db.Carts.AddAsync(cart, cancellationToken);

    public Task<Cart?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Cart?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

    public Task<Cart?> GetByAnonymousIdAsync(Guid anonymousId, CancellationToken cancellationToken = default) =>
        _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.AnonymousId == anonymousId, cancellationToken);
}
