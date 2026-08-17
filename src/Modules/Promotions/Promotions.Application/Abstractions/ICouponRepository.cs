using Promotions.Domain;

namespace Promotions.Application.Abstractions;

public interface ICouponRepository
{
    Task AddAsync(Coupon coupon, CancellationToken cancellationToken = default);

    Task<Coupon?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}

public interface IPromotionsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
