using System.Security.Cryptography;

namespace Store.Web.Middleware;

/// <summary>
/// Adds baseline security headers to every response.
///
/// Unlike the JSON APIs elsewhere in this workspace, this application renders HTML, so a
/// Content-Security-Policy is worth having here rather than being redundant — CSP is the main
/// structural defence against an injected script, and the one mitigation that still helps when
/// auth tokens live somewhere reachable from JavaScript.
///
/// <para>
/// <b>The policy is enforced, not Report-Only.</b> It shipped in Report-Only mode for a while
/// because the views could not satisfy <c>script-src 'self'</c>: four inline <c>&lt;script&gt;</c>
/// blocks across three files, plus 61 inline <c>style="…"</c> attributes in 34 views from a
/// purchased theme. Report-Only enumerated that list, and the list is now dealt with — but not in
/// the same way for scripts and styles, because they are not the same risk.
/// </para>
///
/// <para>
/// <b>Scripts: strict.</b> Every response carries a fresh random nonce, and the four inline blocks
/// carry it too (see <c>HttpContextCspExtensions.CspNonce</c>). <c>'unsafe-inline'</c> is
/// deliberately absent from <c>script-src</c>, so an injected <c>&lt;script&gt;</c> is blocked: an
/// attacker cannot guess a per-request nonce. This is the directive that actually stops XSS, and it
/// is not compromised anywhere in this policy.
/// </para>
///
/// <para>
/// <b>Styles: <c>'unsafe-inline'</c>, knowingly.</b> A nonce cannot cover a <c>style="…"</c>
/// attribute — nonces apply to <c>&lt;style&gt;</c> elements, and CSP has no attribute-level
/// equivalent short of hashing all 61 of them and rehashing on every theme edit. The alternative
/// was rewriting 34 views of a purchased theme into classes, which is a large change to working UI
/// with real regression risk and no benefit to the threat that matters. So style-src is relaxed and
/// script-src is not.
/// </para>
///
/// <para>
/// What that costs: CSS injection stays possible for an attacker who can already write markup, and
/// CSS can exfiltrate some data through attribute selectors and background-image URLs. That is a
/// genuine but far smaller exposure than script execution, and it is bounded by
/// <c>connect-src 'self'</c> and <c>img-src</c>. To close it, move the inline styles into
/// stylesheets and delete <c>'unsafe-inline'</c> from <c>StyleSources</c> — nothing else needs to
/// change.
/// </para>
///
/// Violations are still reported to <see cref="Store.Web.Controllers.CspReportController"/> via
/// <c>report-uri</c>; enforcing does not mean going blind, and a report now means something was
/// actually blocked.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Key under which the per-request nonce is published on <see cref="HttpContext.Items"/>.
    /// Read it from a view through <c>Context.CspNonce()</c> rather than by this key.
    /// </summary>
    internal const string NonceItemKey = "csp-nonce";

    /// <summary>
    /// 128 bits, base64. CSP only requires that a nonce be unguessable and unique per response;
    /// this is generated from a CSPRNG so it cannot be predicted from earlier responses.
    /// </summary>
    private const int NonceBytes = 16;

    /// <summary>
    /// Split out so the trade-off is visible in one place. Deleting <c>'unsafe-inline'</c> from
    /// here is the single change that tightens styles once the inline attributes are gone.
    /// </summary>
    private const string StyleSources = "'self' 'unsafe-inline' https://fonts.googleapis.com";

    public async Task InvokeAsync(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(NonceBytes));
        context.Items[NonceItemKey] = nonce;

        var headers = context.Response.Headers;

        // Stops the browser second-guessing Content-Type, which is what turns an uploaded file
        // served as text/plain into an executed script.
        headers["X-Content-Type-Options"] = "nosniff";

        // Clickjacking. Retained alongside frame-ancestors for older browsers that honour this
        // header but not the directive.
        headers["X-Frame-Options"] = "DENY";

        // Sends the origin but not the path to other sites, so cart/checkout URLs containing ids
        // do not leak through outbound links.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        headers["Content-Security-Policy"] = BuildPolicy(nonce);

        await next(context);
    }

    private static string BuildPolicy(string nonce) =>
        "default-src 'self'; " +
        $"script-src 'self' 'nonce-{nonce}'; " +
        $"style-src {StyleSources}; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'; " +
        // report-uri is deprecated in favour of the Reporting API, but it is what actually works
        // across current browsers. CspReportController receives these.
        "report-uri /csp-report";
}

/// <summary>
/// Gives Razor views the per-request CSP nonce.
/// </summary>
public static class HttpContextCspExtensions
{
    /// <summary>
    /// The nonce for this response. Stamp it on every inline <c>&lt;script&gt;</c>:
    /// <c>&lt;script nonce="@Context.CspNonce()"&gt;</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// If <see cref="SecurityHeadersMiddleware"/> has not run. This throws rather than returning
    /// an empty string on purpose: an empty nonce does not degrade gracefully, it silently blocks
    /// every inline script on the page, and the resulting "my JavaScript stopped working" is
    /// miserable to trace back to a missing middleware registration.
    /// </exception>
    public static string CspNonce(this HttpContext context) =>
        context.Items[SecurityHeadersMiddleware.NonceItemKey] as string
        ?? throw new InvalidOperationException(
            $"No CSP nonce on this request. {nameof(SecurityHeadersMiddleware)} must be " +
            "registered before the endpoint that renders the view.");
}
