using Inventory.Application.Stock;
using Inventory.Domain;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Exceptions;

namespace Inventory.Infrastructure.Repositories;

internal sealed class StockItemRepository : IStockItemRepository
{
    private readonly InventoryDbContext _db;

    public StockItemRepository(InventoryDbContext db) => _db = db;

    public async Task AddAsync(StockItem stockItem, CancellationToken cancellationToken = default) =>
        await _db.StockItems.AddAsync(stockItem, cancellationToken);

    public Task<StockItem?> GetByProductVariantIdAsync(Guid productVariantId, CancellationToken cancellationToken = default) =>
        _db.StockItems
            .Include(s => s.Transactions)
            .FirstOrDefaultAsync(s => s.ProductVariantId == productVariantId, cancellationToken);
}

internal sealed class InventoryUnitOfWork : IInventoryUnitOfWork
{
    private readonly InventoryDbContext _db;

    public InventoryUnitOfWork(InventoryDbContext db) => _db = db;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translated here, not left as an EF Core exception, so Application code (and
            // Store.Web's GlobalExceptionHandler) never needs to know EF Core exists — see
            // ReserveStockCommandHandler's doc comment.
            throw new ConflictException(
                "This stock item was updated by another request at the same time. Please retry.", ex);
        }
    }
}
