using Inventory.Domain;

namespace Inventory.Application.Stock;

public interface IStockItemRepository
{
    Task AddAsync(StockItem stockItem, CancellationToken cancellationToken = default);

    Task<StockItem?> GetByProductVariantIdAsync(Guid productVariantId, CancellationToken cancellationToken = default);
}

public interface IInventoryUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
