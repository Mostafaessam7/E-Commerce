using Microsoft.EntityFrameworkCore;
using Promotions.Application.Coupons;
using Promotions.Infrastructure.Persistence;

namespace Promotions.Infrastructure.Repositories;

internal sealed class PromotionsQueries : IPromotionsQueries
{
    private readonly PromotionsDbContext _db;

    public PromotionsQueries(PromotionsDbContext db) => _db = db;

    public async Task<IReadOnlyList<CouponDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.Coupons
            .AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Select(c => new CouponDto(
                c.Id, c.Code, c.DiscountType.ToString(), c.Value, c.Currency, c.IsActive,
                c.ExpiresAtUtc, c.UsageLimit, c.UsageCount, c.MinimumOrderAmount))
            .ToListAsync(cancellationToken);
}
