using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Store.Web.Infrastructure.RateLimiting;

/// <summary>
/// Per-IP fixed-window rate limiting for the two endpoint families actually worth protecting —
/// credential-stuffing/brute-force targets (login/register/password-reset) and the payment
/// webhook receiver. Not a global limiter: the storefront/admin's normal read traffic has no
/// abuse profile that calls for throttling, and a blanket limiter would just be a source of false
/// positives for real shoppers.
/// </summary>
public static class RateLimiterExtensions
{
    public const string AuthPolicy = "auth";
    public const string WebhookPolicy = "webhook";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // 429 with a plain body — no ProblemDetails wiring needed here, this is a security
            // control, not a business-rule failure the rest of the app's error handling cares
            // about shaping.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Login/Register/ForgotPassword/ResetPassword: 10 attempts per 5 minutes per IP.
            // Generous enough that a real user fat-fingering a password a few times never hits
            // it, tight enough to blunt a credential-stuffing script — ASP.NET Identity's own
            // account lockout (5 failed attempts, 15 min — docs/security.md) still does the
            // per-account defense-in-depth; this is the per-IP layer in front of it.
            options.AddPolicy(AuthPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(5),
                    PermitLimit = 10,
                    QueueLimit = 0,
                }));

            // The webhook endpoint already verifies an HMAC signature before anything else runs
            // (docs/security.md) — this isn't about forged requests, it's about a redelivery
            // storm (a provider's retry policy gone wrong, or a real DoS attempt) burning CPU on
            // signature checks. Generous window: real providers can legitimately burst-retry.
            options.AddPolicy(WebhookPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 30,
                    QueueLimit = 0,
                }));
        });

        return services;
    }
}
