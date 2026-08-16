namespace Payments.Domain;

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    PartiallyRefunded = 3,
    Refunded = 4,
}
