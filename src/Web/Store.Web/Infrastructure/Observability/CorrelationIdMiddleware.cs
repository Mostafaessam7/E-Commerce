using Observability;

namespace Store.Web.Infrastructure.Observability;

/// <summary>
/// Runs first in the pipeline (before routing/auth/exception handling — see Program.cs) so every
/// log line for a request, from any component using <c>ILogger</c> (including
/// <see cref="Store.Web.Infrastructure.ExceptionHandling.GlobalExceptionHandler"/> and EF Core's
/// own command logging), gets the same <see cref="LogContextKeys.CorrelationId"/> without each
/// call site having to fetch <see cref="ICorrelationIdProvider"/> itself. Also echoes the id back
/// on the response (<c>X-Correlation-Id</c>) so a client/support ticket can be matched to a log
/// line without needing server access.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string ResponseHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdProvider correlationIdProvider)
    {
        var correlationId = correlationIdProvider.CorrelationId;
        context.Response.Headers[ResponseHeader] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   [LogContextKeys.CorrelationId] = correlationId,
               }))
        {
            await _next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}
