using System.Net;
using FluentAssertions;

namespace EndToEndTests;

/// <summary>
/// Proves the per-IP rate limiter on auth endpoints (Phase 26) is actually wired into the real
/// pipeline, not just registered — the 11th login attempt inside the same 5-minute window from
/// the same IP must be rejected with 429, not silently processed like the first 10 were.
/// </summary>
public sealed class RateLimitingTests : IClassFixture<StoreWebApplicationFactory>
{
    private readonly StoreWebApplicationFactory _factory;

    public RateLimitingTests(StoreWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_endpoint_returns_429_after_the_per_IP_limit_is_exceeded()
    {
        using var client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var loginPageHtml = await (await client.GetAsync("/Account/Login")).Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex.Match(
            loginPageHtml, """name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)""").Groups[1].Value;

        HttpResponseMessage? lastResponse = null;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            lastResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "nobody@example.com",
                ["Password"] = "WrongPassword#1",
                ["__RequestVerificationToken"] = token,
            }));
        }

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "the 11th attempt in the same window must be throttled");
    }
}
