using Shipping.Contracts;
using Shipping.Domain;

namespace Shipping.Application.Abstractions;

public interface IShippingMethodRepository
{
    Task AddAsync(ShippingMethod method, CancellationToken cancellationToken = default);

    Task<ShippingMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IShippingUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IShippingQueries
{
    Task<IReadOnlyList<ShippingMethodDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);
}
