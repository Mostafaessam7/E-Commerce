using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Store.Web.Controllers;

/// <summary>
/// Receives Content-Security-Policy violation reports.
/// </summary>
/// <remarks>
/// The CSP here runs in Report-Only mode so the storefront keeps working while the inline styles
/// and scripts it still relies on are enumerated. That design only pays off if something actually
/// collects the reports — without this endpoint the browser computed every violation and threw it
/// away, so the list of what has to move out of the markup was never getting written down and the
/// policy could never be enforced.
///
/// Deliberately unauthenticated: the browser posts these with no credentials, from a page that may
/// itself be anonymous. That makes it writable by anyone who can reach it, so it is treated as
/// untrusted input throughout — see the size cap and the logging note below.
/// </remarks>
[ApiController]
[Route("csp-report")]
public sealed class CspReportController(ILogger<CspReportController> logger) : ControllerBase
{
    /// <summary>
    /// Reports larger than this are dropped unread. A violation report is a small JSON object; a
    /// large body is either broken or someone using an unauthenticated endpoint to write to the
    /// logs. 8 KB is well above anything a browser sends.
    /// </summary>
    private const int MaxReportBytes = 8 * 1024;

    private readonly ILogger<CspReportController> _logger = logger;

    [HttpPost]
    [AllowAnonymous]
    [Consumes("application/csp-report", "application/json")]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        // Always 204, whatever happens below. The browser has nothing useful to do with an error,
        // and a distinguishable failure response would tell someone probing this endpoint which
        // inputs get through.
        if (Request.ContentLength > MaxReportBytes)
        {
            return NoContent();
        }

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body) || body.Length > MaxReportBytes)
        {
            return NoContent();
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("csp-report", out var report))
            {
                return NoContent();
            }

            // Guarded because this endpoint is unauthenticated and can be posted to at any rate:
            // extracting four fields per report is cheap individually but pointless work if
            // Information logging is switched off, and the volume is not under our control.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                // Only these four fields are read, rather than logging the whole payload. The
                // report also carries the full page URL and referrer, which on this site can
                // include cart and checkout paths containing ids — that does not belong in logs
                // just to find out a stylesheet was inline. `blocked-uri` and `document-uri` are
                // attacker-influenced, so they are passed as structured parameters and never
                // concatenated into the message.
                _logger.LogInformation(
                    "CSP violation: {ViolatedDirective} blocked {BlockedUri} on {DocumentUri} (source: {SourceFile})",
                    GetString(report, "violated-directive"),
                    GetString(report, "blocked-uri"),
                    GetString(report, "document-uri"),
                    GetString(report, "source-file"));
            }
        }
        catch (JsonException)
        {
            // Unparseable body. Nothing to report and nothing to fix — dropping it keeps a
            // malformed-payload flood out of the logs.
        }

        return NoContent();
    }

    private static string GetString(JsonElement report, string property) =>
        report.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "(none)"
            : "(none)";
}
