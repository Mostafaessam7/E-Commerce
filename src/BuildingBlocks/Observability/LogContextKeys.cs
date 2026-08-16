namespace Observability;

/// <summary>
/// Canonical property names for structured logging (Serilog <c>LogContext.PushProperty</c> /
/// <c>ILogger</c> scopes, wired up in Phase 12). Every module pushes the ids relevant to it under
/// these exact names so logs stay correlatable across modules — e.g. Ordering pushes
/// <see cref="OrderId"/>, Payments pushes <see cref="PaymentId"/>, both alongside the same
/// <see cref="CorrelationId"/> for one checkout request.
/// </summary>
public static class LogContextKeys
{
    public const string CorrelationId = "CorrelationId";
    public const string RequestId = "RequestId";
    public const string UserId = "UserId";
    public const string OrderId = "OrderId";
    public const string PaymentId = "PaymentId";
}
