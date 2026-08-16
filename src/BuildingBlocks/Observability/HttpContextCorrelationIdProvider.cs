using Microsoft.AspNetCore.Http;

namespace Observability;

/// <summary>
/// Reads the correlation id from <c>HttpContext.TraceIdentifier</c>, or from an inbound
/// <c>X-Correlation-Id</c> header when a caller (e.g. an API gateway, another service) already
/// established one upstream. Falls back to a fresh id for non-HTTP contexts (Store.Worker
/// background jobs) where <see cref="IHttpContextAccessor.HttpContext"/> is null.
/// </summary>
public sealed class HttpContextCorrelationIdProvider : ICorrelationIdProvider
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly Lazy<string> _correlationId;

    public HttpContextCorrelationIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        _correlationId = new Lazy<string>(() =>
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null)
            {
                return Guid.NewGuid().ToString();
            }

            if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValue) &&
                !string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue.ToString();
            }

            return context.TraceIdentifier;
        });
    }

    public string CorrelationId => _correlationId.Value;
}
