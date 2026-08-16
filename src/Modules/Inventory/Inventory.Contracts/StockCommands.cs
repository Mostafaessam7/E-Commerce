using Messaging;

namespace Inventory.Contracts;

/// <summary>
/// Reserves stock for one order line at checkout. Lives in Contracts, not Application, because
/// other modules (Ordering) dispatch it directly through the shared <c>IDispatcher</c> — see
/// ADR-014: a module's Contracts project is where commands/queries meant for cross-module
/// dispatch live; Application hosts the handler that implements them.
/// <see cref="ReferenceId"/> is typically the OrderId — kept as a plain Guid, never a reference
/// to Ordering's aggregate (module boundary rules).
/// </summary>
public sealed record ReserveStockCommand(Guid ProductVariantId, int Quantity, Guid? ReferenceId) : ICommand<Unit>;

/// <summary>Compensating action for a partially-succeeded multi-item reservation (see
/// Ordering's PlaceOrderCommand) or an abandoned cart.</summary>
public sealed record ReleaseStockCommand(Guid ProductVariantId, int Quantity, Guid? ReferenceId) : ICommand<Unit>;
