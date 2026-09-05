using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Store.Web.Middleware;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// The CSP is now enforced rather than Report-Only, which means a mistake here does not produce a
/// log line — it produces a storefront with dead JavaScript. Nothing covered this before.
///
/// Two failure modes matter and both are cheap to pin:
///
/// 1. The policy quietly weakening. <c>script-src</c> is the directive doing the actual work, and
///    an <c>'unsafe-inline'</c> added there "to fix something" would leave the header looking
///    healthy while defeating it entirely.
/// 2. The nonce in the header not matching the nonce in the markup, or an inline script being
///    added later without one. Either blocks that script in every browser, and the page fails in
///    a way that looks nothing like a CSP problem.
/// </summary>
public class ContentSecurityPolicyTests
{
    private static readonly Regex InlineScriptWithoutNonce = new(
        @"<script(?![^>]*\bsrc=)(?![^>]*\bnonce=)[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static async Task<HttpContext> InvokeAsync()
    {
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);
        return context;
    }

    private static async Task<string> PolicyAsync() =>
        (await InvokeAsync()).Response.Headers["Content-Security-Policy"].ToString();

    [Fact]
    public async Task The_policy_is_enforced_not_report_only()
    {
        var context = await InvokeAsync();

        Assert.False(
            context.Response.Headers.ContainsKey("Content-Security-Policy-Report-Only"),
            "Report-Only was left behind. Two policies means the enforced one is easy to " +
            "misread as inactive.");
        Assert.True(context.Response.Headers.ContainsKey("Content-Security-Policy"));
    }

    [Fact]
    public async Task Script_src_never_allows_inline()
    {
        var policy = await PolicyAsync();
        var scriptSrc = Directive(policy, "script-src");

        // This is the assertion that matters. A nonce plus 'unsafe-inline' is not a compromise:
        // browsers ignore 'unsafe-inline' when a nonce is present in modern CSP, but older ones
        // honour it, so it would silently re-open script injection for exactly the clients least
        // able to defend themselves.
        Assert.DoesNotContain("'unsafe-inline'", scriptSrc);
        Assert.DoesNotContain("'unsafe-eval'", scriptSrc);
        Assert.Contains("'nonce-", scriptSrc);
    }

    [Fact]
    public async Task Style_src_relaxation_stays_confined_to_styles()
    {
        var policy = await PolicyAsync();

        // 'unsafe-inline' is accepted for styles and documented on the middleware. The point here
        // is that it appears once and only under style-src -- if it ever shows up twice, it has
        // leaked into another directive.
        Assert.Contains("'unsafe-inline'", Directive(policy, "style-src"));
        Assert.Single(Regex.Matches(policy, "'unsafe-inline'"));
    }

    [Fact]
    public async Task The_nonce_in_the_header_is_the_one_views_will_render()
    {
        var context = await InvokeAsync();

        var fromView = context.CspNonce();
        var inHeader = Directive(
            context.Response.Headers["Content-Security-Policy"].ToString(), "script-src");

        // If these ever diverge, every inline script on every page is blocked.
        Assert.Contains($"'nonce-{fromView}'", inHeader);
    }

    [Fact]
    public async Task Each_response_gets_a_fresh_nonce()
    {
        var nonces = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            nonces.Add((await InvokeAsync()).CspNonce());
        }

        // A reused nonce is a guessable nonce, which is the same as having none at all.
        Assert.Equal(50, nonces.Count);
    }

    [Fact]
    public void Reading_the_nonce_without_the_middleware_fails_loudly()
    {
        // Returning "" here would render nonce="" and block the script, with nothing to point at
        // the cause. The exception names the missing registration.
        var ex = Assert.Throws<InvalidOperationException>(() => new DefaultHttpContext().CspNonce());
        Assert.Contains(nameof(SecurityHeadersMiddleware), ex.Message);
    }

    [Fact]
    public void Every_inline_script_in_a_view_carries_the_nonce()
    {
        var views = Directory.EnumerateFiles(
            Path.Combine(SolutionRoot.Path, "src", "Web", "Store.Web"),
            "*.cshtml",
            SearchOption.AllDirectories);

        var offenders = new List<string>();
        foreach (var view in views)
        {
            if (InlineScriptWithoutNonce.IsMatch(File.ReadAllText(view)))
            {
                offenders.Add(Path.GetRelativePath(SolutionRoot.Path, view));
            }
        }

        // The static half of the pair: the middleware can be perfect and the page still break if
        // someone adds an inline <script> and forgets nonce="@Context.CspNonce()".
        Assert.True(
            offenders.Count == 0,
            "These views have an inline <script> with no nonce, so it will be blocked by the " +
            "enforced CSP:\n  " + string.Join("\n  ", offenders) +
            "\n\nAdd nonce=\"@Context.CspNonce()\" to the tag, or move the code into a .js file.");
    }

    private static string Directive(string policy, string name) =>
        policy.Split(';')
            .Select(part => part.Trim())
            .FirstOrDefault(part => part.StartsWith(name + " ", StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"No '{name}' directive in policy: {policy}");
}
