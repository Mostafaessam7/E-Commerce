using SharedKernel.Primitives;

namespace Payments.Domain.Events;

public sealed record PaymentSucceededDomainEvent(Guid PaymentTransactionId, Guid OrderId) : DomainEvent;

public sealed record PaymentFailedDomainEvent(Guid PaymentTransactionId, Guid OrderId, string Reason) : DomainEvent;

public sealed record PaymentRefundedDomainEvent(Guid PaymentTransactionId, Guid OrderId, decimal Amount, bool IsFullRefund) : DomainEvent;
