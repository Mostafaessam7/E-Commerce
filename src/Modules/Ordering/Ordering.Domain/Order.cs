using Ordering.Domain.Events;
using Ordering.Domain.ValueObjects;
using SharedKernel.Exceptions;
using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;
using SharedKernel.ValueObjects;

namespace Ordering.Domain;

/// <summary>
/// Aggregate root for a placed order (Section 8). Status transitions are exclusively through the
/// named methods below — never a public setter — each one validates the transition is legal from
/// the current state and appends to <see cref="StatusHistory"/>. <c>order.Status = ...</c> from
/// outside this class is not possible; that's the point (Section 8's explicit "ممنوع" example).
/// </summary>
public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusHistoryEntry> _statusHistory = [];

    private Order(
        Guid id,
        string orderNumber,
        Guid? customerId,
        string email,
        Address billingAddress,
        Address shippingAddress,
        Money shippingCost,
        Money tax,
        Money discount,
        string? notes,
        DateTime placedAtUtc)
        : base(id)
    {
        OrderNumber = orderNumber;
        CustomerId = customerId;
        Email = email;
        BillingAddress = billingAddress;
        ShippingAddress = shippingAddress;
        ShippingCost = shippingCost;
        Tax = tax;
        Discount = discount;
        Notes = notes;
        Status = OrderStatus.Pending;
        PaymentStatus = PaymentStatus.Pending;
        FulfillmentStatus = FulfillmentStatus.Unfulfilled;

        AppendHistory(OrderStatus.Pending, placedAtUtc, "Order placed.");
    }

    private Order()
    {
    }

    public string OrderNumber { get; private set; } = null!;

    /// <summary>Collected explicitly at checkout, regardless of guest/authenticated — nothing
    /// else in Ordering carries an email (Address doesn't; guests have no ApplicationUser to look
    /// one up from). Notifications' order-confirmation handler reads this straight off
    /// <c>OrderPlacedIntegrationEvent</c> instead of a second cross-module round trip.</summary>
    public string Email { get; private set; } = null!;

    public Guid? CustomerId { get; private set; }

    public Address BillingAddress { get; private set; } = null!;

    public Address ShippingAddress { get; private set; } = null!;

    public string? Notes { get; private set; }

    public OrderStatus Status { get; private set; }

    public PaymentStatus PaymentStatus { get; private set; }

    public FulfillmentStatus FulfillmentStatus { get; private set; }

    public string? TrackingNumber { get; private set; }

    public Money ShippingCost { get; private set; } = null!;

    public Money Tax { get; private set; } = null!;

    public Money Discount { get; private set; } = null!;

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public IReadOnlyCollection<OrderStatusHistoryEntry> StatusHistory => _statusHistory.AsReadOnly();

    public Money Subtotal => _items
        .Select(i => i.LineTotal)
        .Aggregate((Money?)null, (total, line) => total is null ? line : total.Add(line))
        ?? Money.Zero(ShippingCost.Currency);

    public Money Total => Subtotal.Add(ShippingCost).Add(Tax).Subtract(Discount);

    /// <summary>
    /// Creates a placed order from checkout-time line items (already re-validated by the caller —
    /// see Ordering.Application's PlaceOrderCommand). Every line is a permanent snapshot
    /// (<see cref="OrderItem"/>): later Product price changes never retroactively affect this order.
    /// </summary>
    public static Result<Order> Place(
        string orderNumber,
        Guid? customerId,
        string email,
        Address billingAddress,
        Address shippingAddress,
        IReadOnlyCollection<(Guid ProductVariantId, Guid ProductId, string ProductName, string Sku, decimal UnitPrice, string Currency, int Quantity)> items,
        decimal shippingCost,
        decimal tax,
        decimal discount,
        string currency,
        string? notes,
        DateTime placedAtUtc)
    {
        Guard.Against.NullOrWhiteSpace(orderNumber, nameof(orderNumber));
        Guard.Against.NullOrWhiteSpace(email, nameof(email));

        if (items.Count == 0)
        {
            return Result.Failure<Order>(Error.Validation("Order.Empty", "An order must have at least one item."));
        }

        var shippingResult = Money.Create(shippingCost, currency);
        var taxResult = Money.Create(tax, currency);
        var discountResult = Money.Create(discount, currency);

        foreach (var result in new[] { shippingResult.Error, taxResult.Error, discountResult.Error })
        {
            if (result != Error.None)
            {
                return Result.Failure<Order>(result);
            }
        }

        var order = new Order(
            Guid.NewGuid(), orderNumber, customerId, email, billingAddress, shippingAddress,
            shippingResult.Value, taxResult.Value, discountResult.Value, notes, placedAtUtc);

        foreach (var item in items)
        {
            var priceResult = Money.Create(item.UnitPrice, item.Currency);
            if (priceResult.IsFailure)
            {
                return Result.Failure<Order>(priceResult.Error);
            }

            order._items.Add(new OrderItem(
                Guid.NewGuid(), item.ProductVariantId, item.ProductId, item.ProductName, item.Sku, priceResult.Value, item.Quantity));
        }

        order.RaiseDomainEvent(new OrderPlacedDomainEvent(order.Id));
        return Result.Success(order);
    }

    public Result Confirm(DateTime occurredAtUtc)
    {
        if (Status != OrderStatus.Pending)
        {
            return Result.Failure(Error.Conflict("Order.CannotConfirm", $"Cannot confirm an order in status '{Status}'."));
        }

        Status = OrderStatus.Confirmed;
        AppendHistory(Status, occurredAtUtc, "Order confirmed.");
        return Result.Success();
    }

    public Result MarkAsPaid(DateTime occurredAtUtc)
    {
        if (PaymentStatus == PaymentStatus.Paid)
        {
            return Result.Failure(Error.Conflict("Order.AlreadyPaid", "This order has already been paid."));
        }

        if (Status is OrderStatus.Cancelled)
        {
            return Result.Failure(Error.Conflict("Order.Cancelled", "Cannot mark a cancelled order as paid."));
        }

        PaymentStatus = PaymentStatus.Paid;
        if (Status == OrderStatus.Pending)
        {
            Status = OrderStatus.Confirmed;
        }

        AppendHistory(Status, occurredAtUtc, "Payment received.");
        RaiseDomainEvent(new OrderPaidDomainEvent(Id));
        return Result.Success();
    }

    public Result StartProcessing(DateTime occurredAtUtc)
    {
        if (Status != OrderStatus.Confirmed)
        {
            return Result.Failure(Error.Conflict("Order.CannotProcess", $"Cannot start processing an order in status '{Status}'."));
        }

        Status = OrderStatus.Processing;
        AppendHistory(Status, occurredAtUtc, "Order is being processed.");
        return Result.Success();
    }

    public Result MarkAsShipped(DateTime occurredAtUtc, string? trackingNumber = null)
    {
        if (Status != OrderStatus.Processing)
        {
            return Result.Failure(Error.Conflict("Order.CannotShip", $"Cannot ship an order in status '{Status}'."));
        }

        Status = OrderStatus.Shipped;
        FulfillmentStatus = FulfillmentStatus.Fulfilled;
        TrackingNumber = trackingNumber;
        AppendHistory(Status, occurredAtUtc, trackingNumber is null ? "Order shipped." : $"Order shipped (tracking: {trackingNumber}).");
        RaiseDomainEvent(new OrderShippedDomainEvent(Id, trackingNumber));
        return Result.Success();
    }

    public Result MarkAsDelivered(DateTime occurredAtUtc)
    {
        if (Status != OrderStatus.Shipped)
        {
            return Result.Failure(Error.Conflict("Order.CannotDeliver", $"Cannot deliver an order in status '{Status}'."));
        }

        Status = OrderStatus.Delivered;
        AppendHistory(Status, occurredAtUtc, "Order delivered.");
        return Result.Success();
    }

    public Result Cancel(string reason, DateTime occurredAtUtc)
    {
        Guard.Against.NullOrWhiteSpace(reason, nameof(reason));

        if (Status is OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.Cancelled)
        {
            return Result.Failure(Error.Conflict("Order.CannotCancel", $"Cannot cancel an order in status '{Status}'."));
        }

        Status = OrderStatus.Cancelled;
        AppendHistory(Status, occurredAtUtc, reason);
        RaiseDomainEvent(new OrderCancelledDomainEvent(Id, reason));
        return Result.Success();
    }

    private void AppendHistory(OrderStatus status, DateTime occurredAtUtc, string? note)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Order status history timestamps must be UTC.");
        }

        _statusHistory.Add(new OrderStatusHistoryEntry(Guid.NewGuid(), status, occurredAtUtc, note));
    }
}
