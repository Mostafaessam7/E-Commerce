namespace Messaging;

/// <summary>
/// Marker for "no meaningful return value" — <see cref="IRequestHandler{TRequest,TResponse}"/>
/// always needs a TResponse, and a command whose only interesting outcome is success/failure
/// still needs one to flow through the same <c>Result&lt;T&gt;</c> pipeline as every other
/// request. Generic (not module-specific) because any module's fire-and-forget-style command can
/// need it — first shared across modules when Ordering started dispatching Inventory's
/// <c>ReserveStockCommand</c> (Phase 7/8).
/// </summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
