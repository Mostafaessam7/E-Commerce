using FluentAssertions;
using Inventory.Domain;
using Inventory.Domain.Events;

namespace UnitTests.Inventory;

public class StockItemTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static StockItem CreateStock(int quantity = 10, int lowStockThreshold = 2, bool allowBackorder = false) =>
        StockItem.Create(Guid.NewGuid(), quantity, lowStockThreshold, allowBackorder).Value;

    [Fact]
    public void Reserve_succeeds_when_enough_stock_is_available()
    {
        var stock = CreateStock(quantity: 10);

        var result = stock.Reserve(4, Now);

        result.IsSuccess.Should().BeTrue();
        stock.QuantityReserved.Should().Be(4);
        stock.AvailableQuantity.Should().Be(6);
    }

    [Fact]
    public void Reserve_fails_instead_of_overselling_when_not_enough_stock_and_no_backorder()
    {
        var stock = CreateStock(quantity: 3, allowBackorder: false);

        var result = stock.Reserve(5, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockItem.InsufficientStock");
        stock.QuantityReserved.Should().Be(0, "a failed reservation must not partially reserve stock");
    }

    [Fact]
    public void Reserve_raises_StockReservationFailedDomainEvent_when_it_fails()
    {
        var stock = CreateStock(quantity: 1, allowBackorder: false);

        stock.Reserve(5, Now);

        stock.DomainEvents.Should().ContainSingle(e => e is StockReservationFailedDomainEvent);
    }

    [Fact]
    public void Reserve_succeeds_beyond_available_quantity_when_backorder_is_allowed()
    {
        var stock = CreateStock(quantity: 1, allowBackorder: true);

        var result = stock.Reserve(5, Now);

        result.IsSuccess.Should().BeTrue();
        stock.QuantityReserved.Should().Be(5);
    }

    [Fact]
    public void Reserve_raises_StockLowDomainEvent_once_available_quantity_drops_to_the_threshold()
    {
        var stock = CreateStock(quantity: 10, lowStockThreshold: 5);

        stock.Reserve(6, Now);

        stock.DomainEvents.Should().ContainSingle(e => e is StockLowDomainEvent);
    }

    [Fact]
    public void Release_returns_reserved_stock_to_available()
    {
        var stock = CreateStock(quantity: 10);
        stock.Reserve(4, Now);

        var result = stock.Release(4, Now);

        result.IsSuccess.Should().BeTrue();
        stock.QuantityReserved.Should().Be(0);
        stock.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public void Release_fails_when_releasing_more_than_is_reserved()
    {
        var stock = CreateStock(quantity: 10);
        stock.Reserve(2, Now);

        var result = stock.Release(5, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockItem.ReleaseExceedsReserved");
    }

    [Fact]
    public void Confirm_deducts_from_both_reserved_and_on_hand()
    {
        var stock = CreateStock(quantity: 10);
        stock.Reserve(4, Now);

        var result = stock.Confirm(4, Now);

        result.IsSuccess.Should().BeTrue();
        stock.QuantityOnHand.Should().Be(6);
        stock.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public void Receive_increases_on_hand_quantity()
    {
        var stock = CreateStock(quantity: 10);

        stock.Receive(5, Now, "PO-123");

        stock.QuantityOnHand.Should().Be(15);
        stock.Transactions.Should().ContainSingle(t => t.Type == StockTransactionType.Received && t.Quantity == 5);
    }

    [Fact]
    public void AdjustTo_fails_when_the_new_quantity_is_below_what_is_already_reserved()
    {
        var stock = CreateStock(quantity: 10);
        stock.Reserve(8, Now);

        var result = stock.AdjustTo(5, "stock count correction", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockItem.AdjustmentBelowReserved");
    }

    [Fact]
    public void IsOutOfStock_is_true_when_nothing_is_available_and_backorder_is_disallowed()
    {
        var stock = CreateStock(quantity: 3, allowBackorder: false);
        stock.Reserve(3, Now);

        stock.IsOutOfStock.Should().BeTrue();
    }

    [Fact]
    public void IsOutOfStock_is_false_when_backorder_is_allowed_even_at_zero_available()
    {
        var stock = CreateStock(quantity: 3, allowBackorder: true);
        stock.Reserve(3, Now);

        stock.IsOutOfStock.Should().BeFalse();
    }
}
