using Messaging;

namespace Ordering.Contracts;

public sealed record OrderContactInfoDto(Guid OrderId, string OrderNumber, string Email, decimal Total, string Currency);

/// <summary>
/// The one piece of Order data Notifications needs but a payment webhook's own event doesn't
/// carry (<c>PaymentSucceededIntegrationEvent</c> has no email — see that record) — dispatched via
/// the shared <c>IDispatcher</c> (ADR-014), never a direct reference to Ordering.Application.
/// </summary>
public sealed record GetOrderContactInfoQuery(Guid OrderId) : IQuery<OrderContactInfoDto>;
