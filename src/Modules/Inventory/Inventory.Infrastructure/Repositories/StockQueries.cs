using Inventory.Application.Stock;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Repositories;

internal sealed class StockQueries : IStockQueries
{
    private readonly InventoryDbContext _db;

    public StockQueries(InventoryDbContext db) => _db = db;

    public async Task<StockSearchResultDto> SearchAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _db.StockItems.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.ProductVariantId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StockSummaryDto(
                s.Id, s.ProductVariantId, s.QuantityOnHand, s.QuantityReserved,
                s.QuantityOnHand - s.QuantityReserved, s.LowStockThreshold,
                s.QuantityOnHand - s.QuantityReserved <= 0 && !s.AllowBackorder))
            .ToListAsync(cancellationToken);

        return new StockSearchResultDto(items, totalCount, page, pageSize);
    }
}
