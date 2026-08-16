namespace Observability;

/// <summary>
/// A single id that ties one request together across every log line it produces, regardless of
/// which module handled which part of it. Full distributed tracing (OpenTelemetry spans, metrics,
/// exporters) is Phase 12; this narrower abstraction ships in Phase 1 because every later phase's
/// logging statements want to stamp <see cref="CorrelationId"/> on their structured logs from day
/// one rather than retrofitting it later.
/// </summary>
public interface ICorrelationIdProvider
{
    string CorrelationId { get; }
}
