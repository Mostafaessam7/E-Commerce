using SharedKernel.Primitives;

namespace Inventory.Domain;

/// <summary>One row of Section 5's "Stock Transactions / Inventory History" — appended, never
/// mutated, on every operation that changes a StockItem's quantities.</summary>
public sealed class StockTransaction : Entity<Guid>
{
    internal StockTransaction(Guid id, StockTransactionType type, int quantity, DateTime occurredAtUtc, string? reason, Guid? referenceId)
        : base(id)
    {
        Type = type;
        Quantity = quantity;
        OccurredAtUtc = occurredAtUtc;
        Reason = reason;
        ReferenceId = referenceId;
    }

    private StockTransaction()
    {
    }

    public StockTransactionType Type { get; private set; }

    /// <summary>Signed: positive for receipts/releases (stock becomes more available), negative
    /// for reservations/confirmed deductions.</summary>
    public int Quantity { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string? Reason { get; private set; }

    /// <summary>E.g. the OrderId a reservation was made for — a plain Guid reference to another
    /// module's aggregate, never a navigation (see docs/architecture.md).</summary>
    public Guid? ReferenceId { get; private set; }
}
