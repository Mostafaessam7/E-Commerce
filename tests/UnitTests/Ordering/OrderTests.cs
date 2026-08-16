using FluentAssertions;
using Ordering.Domain;
using Ordering.Domain.Events;
using Ordering.Domain.ValueObjects;

namespace UnitTests.Ordering;

public class OrderTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Address TestAddress() =>
        Address.Create("Ahmed Ali", "+201000000000", "1 Test St", null, "Cairo", null, "11511", "EG").Value;

    private static Order CreateOrder(int quantity = 1)
    {
        var items = new List<(Guid, Guid, string, string, decimal, string, int)>
        {
            (Guid.NewGuid(), Guid.NewGuid(), "Nike T-Shirt", "NIKE-BLK-M", 500m, "EGP", quantity),
        };

        return Order.Place(
            "ORD-TEST-1", Guid.NewGuid(), "buyer@example.com", TestAddress(), TestAddress(),
            items, shippingCost: 50m, tax: 25m, discount: 0m, currency: "EGP", notes: null, placedAtUtc: Now).Value;
    }

    [Fact]
    public void Place_fails_for_an_order_with_no_items()
    {
        var result = Order.Place(
            "ORD-EMPTY", Guid.NewGuid(), "buyer@example.com", TestAddress(), TestAddress(),
            [], shippingCost: 0m, tax: 0m, discount: 0m, currency: "EGP", notes: null, placedAtUtc: Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.Empty");
    }

    [Fact]
    public void Place_computes_the_total_from_subtotal_shipping_tax_and_discount()
    {
        var order = CreateOrder(quantity: 2);

        order.Subtotal.Amount.Should().Be(1000m);
        order.Total.Amount.Should().Be(1000m + 50m + 25m);
    }

    [Fact]
    public void Place_starts_Pending_with_PaymentStatus_Pending_and_raises_OrderPlacedDomainEvent()
    {
        var order = CreateOrder();

        order.Status.Should().Be(OrderStatus.Pending);
        order.PaymentStatus.Should().Be(PaymentStatus.Pending);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPlacedDomainEvent);
    }

    [Fact]
    public void MarkAsPaid_moves_a_Pending_order_to_Confirmed_and_raises_OrderPaidDomainEvent()
    {
        var order = CreateOrder();

        var result = order.MarkAsPaid(Now);

        result.IsSuccess.Should().BeTrue();
        order.PaymentStatus.Should().Be(PaymentStatus.Paid);
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaidDomainEvent);
    }

    [Fact]
    public void Cannot_ship_an_order_that_has_not_started_processing()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Now);

        var result = order.MarkAsShipped(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.CannotShip");
    }

    [Fact]
    public void Full_happy_path_confirm_process_ship_deliver()
    {
        var order = CreateOrder();

        order.MarkAsPaid(Now).IsSuccess.Should().BeTrue();
        order.StartProcessing(Now).IsSuccess.Should().BeTrue();
        order.MarkAsShipped(Now, "TRACK-123").IsSuccess.Should().BeTrue();
        order.MarkAsDelivered(Now).IsSuccess.Should().BeTrue();

        order.Status.Should().Be(OrderStatus.Delivered);
        order.FulfillmentStatus.Should().Be(FulfillmentStatus.Fulfilled);
        order.TrackingNumber.Should().Be("TRACK-123");
        order.StatusHistory.Should().HaveCount(5, "Pending + Confirmed + Processing + Shipped + Delivered");
    }

    [Fact]
    public void Cancel_fails_once_an_order_has_shipped()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Now);
        order.StartProcessing(Now);
        order.MarkAsShipped(Now);

        var result = order.Cancel("Customer changed their mind", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.CannotCancel");
    }

    [Fact]
    public void Cancel_succeeds_before_shipping_and_raises_OrderCancelledDomainEvent()
    {
        var order = CreateOrder();

        var result = order.Cancel("Out of stock", Now);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledDomainEvent);
    }
}
