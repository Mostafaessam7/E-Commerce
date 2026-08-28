namespace Store.Web.Middleware;

/// <summary>
/// Adds baseline security headers to every response.
///
/// Unlike the JSON APIs elsewhere in this workspace, this application renders HTML, so a
/// Content-Security-Policy is worth having here rather than being redundant — CSP is the main
/// structural defence against an injected script, and the one mitigation that still helps when
/// auth tokens live somewhere reachable from JavaScript.
///
/// That policy ships in <b>Report-Only</b> mode deliberately. The current views use inline
/// <c>style="…"</c> attributes in 34 files and inline <c>&lt;script&gt;</c> blocks in 3, plus a
/// purchased theme; an enforced <c>script-src 'self'; style-src 'self'</c> would break the
/// storefront on the first page load. Report-Only applies the same policy and reports every
/// violation without blocking anything, which turns "we should add a CSP" into a concrete list of
/// what has to move out of the markup first. Switch the header name to
/// <c>Content-Security-Policy</c> once that list is empty — the policy string itself does not need
/// to change.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// 'unsafe-inline' is deliberately absent even though the markup currently needs it: including
    /// it would make the policy pass silently and defeat the point of running Report-Only, which is
    /// to enumerate exactly what still relies on inline code.
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Stops the browser second-guessing Content-Type, which is what turns an uploaded file
        // served as text/plain into an executed script.
        headers["X-Content-Type-Options"] = "nosniff";

        // Clickjacking: nothing here is meant to be embedded in someone else's page. Kept
        // alongside frame-ancestors in the CSP because that directive is Report-Only for now and
        // therefore not actually enforcing.
        headers["X-Frame-Options"] = "DENY";

        // Sends the origin but not the path to other sites, so cart/checkout URLs containing ids
        // do not leak through outbound links.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        headers["Content-Security-Policy-Report-Only"] = ContentSecurityPolicy;

        await next(context);
    }
}
