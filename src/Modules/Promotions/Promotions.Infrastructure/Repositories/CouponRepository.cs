using Microsoft.EntityFrameworkCore;
using Promotions.Application.Abstractions;
using Promotions.Domain;
using Promotions.Infrastructure.Persistence;

namespace Promotions.Infrastructure.Repositories;

internal sealed class CouponRepository : ICouponRepository
{
    private readonly PromotionsDbContext _db;

    public CouponRepository(PromotionsDbContext db) => _db = db;

    public async Task AddAsync(Coupon coupon, CancellationToken cancellationToken = default) =>
        await _db.Coupons.AddAsync(coupon, cancellationToken);

    public Task<Coupon?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.Coupons.FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);
    }
}

internal sealed class PromotionsUnitOfWork : IPromotionsUnitOfWork
{
    private readonly PromotionsDbContext _db;

    public PromotionsUnitOfWork(PromotionsDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
