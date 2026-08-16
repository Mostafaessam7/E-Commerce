using FluentAssertions;
using Inventory.Domain;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Exceptions;

namespace IntegrationTests.Inventory;

/// <summary>
/// Proves the claim in StockItem's doc comment: two concurrent reservations against the last
/// unit of the same variant cannot both succeed. Each "request" gets its own DbContext (matching
/// real scoped-per-request lifetimes) reading the same row, so the second SaveChanges collides on
/// the rowversion token configured in StockItemConfiguration.
/// </summary>
public sealed class StockConcurrencyTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";

    private Guid _stockItemId;
    private readonly Guid _productVariantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        var stockItem = StockItem.Create(_productVariantId, initialQuantity: 1).Value;
        db.StockItems.Add(stockItem);
        await db.SaveChangesAsync();
        _stockItemId = stockItem.Id;
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        var stockItem = await db.StockItems.FindAsync(_stockItemId);
        if (stockItem is not null)
        {
            db.StockItems.Remove(stockItem);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Only_one_of_two_concurrent_reservations_for_the_last_unit_succeeds()
    {
        await using var contextA = CreateContext();
        await using var contextB = CreateContext();

        var stockA = await new StockItemRepository(contextA).GetByProductVariantIdAsync(_productVariantId);
        var stockB = await new StockItemRepository(contextB).GetByProductVariantIdAsync(_productVariantId);

        stockA!.Reserve(1, DateTime.UtcNow).IsSuccess.Should().BeTrue("both read 1 available before either commits");
        stockB!.Reserve(1, DateTime.UtcNow).IsSuccess.Should().BeTrue();

        var unitOfWorkA = new InventoryUnitOfWork(contextA);
        var unitOfWorkB = new InventoryUnitOfWork(contextB);

        await unitOfWorkA.SaveChangesAsync();

        var act = async () => await unitOfWorkB.SaveChangesAsync();
        await act.Should().ThrowAsync<ConflictException>("the row changed under it — optimistic concurrency must not let both reservations win");

        // The unit was not sold twice: exactly one reservation persisted.
        await using var verifyDb = CreateContext();
        var finalState = await verifyDb.StockItems.AsNoTracking().FirstAsync(s => s.Id == _stockItemId);
        finalState.QuantityReserved.Should().Be(1);
    }

    private static InventoryDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlServer(ConnectionString).Options);
}
