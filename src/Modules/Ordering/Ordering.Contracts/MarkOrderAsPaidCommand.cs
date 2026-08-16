using Messaging;

namespace Ordering.Contracts;

/// <summary>
/// The only sanctioned way another module can move an order to PaymentStatus=Paid — Payments
/// dispatches this via the shared <c>IDispatcher</c> once a webhook confirms a successful charge
/// (ADR-014, reverse direction: Payments calling Ordering, same pattern as Ordering calling
/// Catalog/Inventory at checkout). Never a direct reference to Ordering.Domain/Application.
/// </summary>
public sealed record MarkOrderAsPaidCommand(Guid OrderId) : ICommand<Unit>;
