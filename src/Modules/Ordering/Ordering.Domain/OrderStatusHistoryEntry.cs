using SharedKernel.Primitives;

namespace Ordering.Domain;

public sealed class OrderStatusHistoryEntry : Entity<Guid>
{
    internal OrderStatusHistoryEntry(Guid id, OrderStatus status, DateTime occurredAtUtc, string? note)
        : base(id)
    {
        Status = status;
        OccurredAtUtc = occurredAtUtc;
        Note = note;
    }

    private OrderStatusHistoryEntry()
    {
    }

    public OrderStatus Status { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string? Note { get; private set; }
}
