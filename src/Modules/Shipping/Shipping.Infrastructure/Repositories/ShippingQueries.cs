using Microsoft.EntityFrameworkCore;
using Shipping.Application.Abstractions;
using Shipping.Contracts;
using Shipping.Infrastructure.Persistence;

namespace Shipping.Infrastructure.Repositories;

internal sealed class ShippingQueries : IShippingQueries
{
    private readonly ShippingDbContext _db;

    public ShippingQueries(ShippingDbContext db) => _db = db;

    public async Task<IReadOnlyList<ShippingMethodDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = _db.ShippingMethods.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(m => m.IsActive);
        }

        return await query
            .OrderBy(m => m.Cost.Amount)
            .Select(m => new ShippingMethodDto(
                m.Id, m.Name, m.Description, m.Cost.Amount, m.Cost.Currency, m.EstimatedDaysMin, m.EstimatedDaysMax, m.IsActive))
            .ToListAsync(cancellationToken);
    }
}
