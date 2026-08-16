using Inventory.Domain.Events;
using SharedKernel.Exceptions;
using SharedKernel.Guards;
using SharedKernel.Primitives;
using SharedKernel.Results;

namespace Inventory.Domain;

/// <summary>
/// Aggregate root owning stock for exactly one <c>Catalog.Domain.ProductVariant</c> — referenced
/// only by <see cref="ProductVariantId"/> (a plain Guid), never a navigation/FK to Catalog's
/// tables (separate DbContext, separate module — see docs/architecture.md).
///
/// Overselling prevention: <see cref="Reserve"/> checks <see cref="AvailableQuantity"/> before
/// committing, and the EF mapping (Inventory.Infrastructure) adds a rowversion concurrency token
/// so two concurrent reservations against the same row can't both read "5 available" and both
/// succeed — the second SaveChanges throws <c>DbUpdateConcurrencyException</c>, which the
/// Application-layer command handler maps to a <see cref="Error.Conflict"/> Result for the caller
/// to retry. Optimistic (not pessimistic/row-locking) because checkout-time stock checks are a
/// small critical section and most attempts don't actually conflict — see ADR-006.
/// </summary>
public sealed class StockItem : AggregateRoot<Guid>
{
    private readonly List<StockTransaction> _transactions = [];

    private StockItem(Guid id, Guid productVariantId, int quantityOnHand, int lowStockThreshold, bool allowBackorder)
        : base(id)
    {
        ProductVariantId = productVariantId;
        QuantityOnHand = quantityOnHand;
        LowStockThreshold = lowStockThreshold;
        AllowBackorder = allowBackorder;
    }

    private StockItem()
    {
    }

    public Guid ProductVariantId { get; private set; }

    public int QuantityOnHand { get; private set; }

    public int QuantityReserved { get; private set; }

    public int LowStockThreshold { get; private set; }

    public bool AllowBackorder { get; private set; }

    public int AvailableQuantity => QuantityOnHand - QuantityReserved;

    public bool IsOutOfStock => AvailableQuantity <= 0 && !AllowBackorder;

    public IReadOnlyCollection<StockTransaction> Transactions => _transactions.AsReadOnly();

    public static Result<StockItem> Create(
        Guid productVariantId, int initialQuantity = 0, int lowStockThreshold = 0, bool allowBackorder = false)
    {
        Guard.Against.Empty(productVariantId, nameof(productVariantId));

        if (initialQuantity < 0)
        {
            return Result.Failure<StockItem>(Error.Validation("StockItem.NegativeQuantity", "Initial quantity cannot be negative."));
        }

        return Result.Success(new StockItem(Guid.NewGuid(), productVariantId, initialQuantity, lowStockThreshold, allowBackorder));
    }

    /// <summary>Reserves stock for a pending order line during checkout. Never oversells: fails
    /// with a Conflict Result if there isn't enough available and backorders aren't allowed.</summary>
    public Result Reserve(int quantity, DateTime occurredAtUtc, Guid? referenceId = null)
    {
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        if (quantity > AvailableQuantity && !AllowBackorder)
        {
            RaiseDomainEvent(new StockReservationFailedDomainEvent(Id, ProductVariantId, quantity));
            return Result.Failure(Error.Conflict(
                "StockItem.InsufficientStock",
                $"Only {AvailableQuantity} unit(s) available, {quantity} requested."));
        }

        QuantityReserved += quantity;
        AddTransaction(StockTransactionType.Reserved, -quantity, occurredAtUtc, reason: null, referenceId);
        RaiseDomainEvent(new StockReservedDomainEvent(Id, ProductVariantId, quantity));
        RaiseLowStockEventIfNeeded();

        return Result.Success();
    }

    /// <summary>Releases a reservation that won't be fulfilled (cart abandoned, order cancelled
    /// before payment) — stock becomes available again.</summary>
    public Result Release(int quantity, DateTime occurredAtUtc, Guid? referenceId = null)
    {
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        if (quantity > QuantityReserved)
        {
            return Result.Failure(Error.Validation(
                "StockItem.ReleaseExceedsReserved", "Cannot release more than is currently reserved."));
        }

        QuantityReserved -= quantity;
        AddTransaction(StockTransactionType.ReservationReleased, quantity, occurredAtUtc, reason: null, referenceId);

        return Result.Success();
    }

    /// <summary>Converts a reservation into a permanent deduction once the order is actually
    /// fulfilled (paid/shipped) — stock physically leaves the warehouse.</summary>
    public Result Confirm(int quantity, DateTime occurredAtUtc, Guid? referenceId = null)
    {
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        if (quantity > QuantityReserved)
        {
            return Result.Failure(Error.Validation(
                "StockItem.ConfirmExceedsReserved", "Cannot confirm more than is currently reserved."));
        }

        QuantityReserved -= quantity;
        QuantityOnHand -= quantity;
        AddTransaction(StockTransactionType.ReservationConfirmed, -quantity, occurredAtUtc, reason: null, referenceId);

        return Result.Success();
    }

    /// <summary>Restocking — a purchase order/supplier delivery arriving.</summary>
    public void Receive(int quantity, DateTime occurredAtUtc, string? reason = null)
    {
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        QuantityOnHand += quantity;
        AddTransaction(StockTransactionType.Received, quantity, occurredAtUtc, reason, referenceId: null);
    }

    /// <summary>Manual correction (stock count, damage, shrinkage) to a known-correct on-hand
    /// quantity — Section 5's "Stock Adjustment".</summary>
    public Result AdjustTo(int newQuantityOnHand, string reason, DateTime occurredAtUtc)
    {
        Guard.Against.NullOrWhiteSpace(reason, nameof(reason));

        if (newQuantityOnHand < 0)
        {
            return Result.Failure(Error.Validation("StockItem.NegativeQuantity", "Quantity cannot be negative."));
        }

        if (newQuantityOnHand < QuantityReserved)
        {
            return Result.Failure(Error.Conflict(
                "StockItem.AdjustmentBelowReserved",
                $"Cannot adjust to {newQuantityOnHand}: {QuantityReserved} unit(s) are already reserved."));
        }

        var delta = newQuantityOnHand - QuantityOnHand;
        QuantityOnHand = newQuantityOnHand;
        AddTransaction(StockTransactionType.Adjusted, delta, occurredAtUtc, reason, referenceId: null);
        RaiseLowStockEventIfNeeded();

        return Result.Success();
    }

    public void SetLowStockThreshold(int threshold)
    {
        if (threshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Threshold cannot be negative.");
        }

        LowStockThreshold = threshold;
    }

    public void SetAllowBackorder(bool allow) => AllowBackorder = allow;

    private void AddTransaction(StockTransactionType type, int quantity, DateTime occurredAtUtc, string? reason, Guid? referenceId)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Stock transaction timestamps must be UTC.");
        }

        _transactions.Add(new StockTransaction(Guid.NewGuid(), type, quantity, occurredAtUtc, reason, referenceId));
    }

    private void RaiseLowStockEventIfNeeded()
    {
        if (AvailableQuantity <= LowStockThreshold)
        {
            RaiseDomainEvent(new StockLowDomainEvent(Id, ProductVariantId, AvailableQuantity, LowStockThreshold));
        }
    }
}
