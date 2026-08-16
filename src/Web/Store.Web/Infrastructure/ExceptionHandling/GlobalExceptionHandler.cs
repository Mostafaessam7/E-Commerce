using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Observability;
using SharedKernel.Exceptions;

namespace Store.Web.Infrastructure.ExceptionHandling;

/// <summary>
/// Last line of defense: catches anything a controller/handler didn't turn into a Result or
/// handle itself, logs it once with full context, and returns RFC 7807 ProblemDetails instead of
/// an ASP.NET Core yellow-screen or a raw stack trace leaking to the client. Section 24 asked
/// for "no try/catch in every controller" — this is that, applied once at the pipeline edge via
/// the built-in <see cref="IExceptionHandler"/> hook (<c>app.UseExceptionHandler()</c> in
/// Program.cs) instead of per-action boilerplate.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Store.Web serves both server-rendered pages (storefront, admin) and JSON endpoints
        // (cart/AJAX, future API). A page navigation that throws should still land on the
        // branded Razor error view, not raw JSON — so this handler only takes over for requests
        // that were already asking for JSON, and defers everything else to the
        // ExceptionHandlingPath fallback configured in Program.cs (`UseExceptionHandler`).
        if (!WantsJson(httpContext.Request))
        {
            return false;
        }

        // Resolved from the request's own scoped container, not the constructor — this handler
        // is registered Singleton (AddExceptionHandler<T>'s default), and ICorrelationIdProvider
        // is Scoped; injecting it directly fails DI validation at startup
        // ("Cannot consume scoped service ... from singleton"). Only needed here for the
        // ProblemDetails payload — the log line below already carries it via
        // Store.Web.Infrastructure.Observability.CorrelationIdMiddleware's request-wide scope.
        var correlationId = httpContext.RequestServices.GetRequiredService<ICorrelationIdProvider>().CorrelationId;
        var statusCode = HttpStatusCodeMapper.FromException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Path}", httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Request to {Path} failed with {StatusCode}", httpContext.Request.Path, statusCode);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = TitleFor(exception),
            Detail = _environment.IsDevelopment() || statusCode < StatusCodes.Status500InternalServerError
                ? exception.Message
                : "An unexpected error occurred. Please try again or contact support.",
            Instance = httpContext.Request.Path,
        };
        problemDetails.Extensions["correlationId"] = correlationId;

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .Select(e => new { e.Code, e.Message })
                .ToArray();
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static bool WantsJson(HttpRequest request) =>
        request.Path.StartsWithSegments("/api") ||
        request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
        request.Headers.Accept.Any(a => a is not null && a.Contains("application/json", StringComparison.OrdinalIgnoreCase));

    private static string TitleFor(Exception exception) => exception switch
    {
        ValidationException => "Validation failed",
        DomainException => "Business rule violated",
        NotFoundException => "Resource not found",
        ConflictException => "Conflict",
        UnauthorizedException => "Authentication required",
        ForbiddenException => "Access denied",
        _ => "An unexpected error occurred",
    };
}
