namespace Store.Web.Infrastructure;

/// <summary>
/// Guest-cart identity: a long-lived cookie value, not a session id — Section 6's guest cart
/// needs to survive across browser restarts, not just one session. Customer-linked carts
/// (<c>GetOrCreateCartCommand</c> with a real CustomerId) will replace this path once Identity's
/// Account/Login UI exists to actually authenticate a user; every cart operation for now goes
/// through the anonymous id, same as an unauthenticated storefront visitor.
/// </summary>
public static class AnonymousIdExtensions
{
    private const string CookieName = "anon_id";

    public static Guid GetOrSetAnonymousId(this HttpContext httpContext)
    {
        if (Guid.TryParse(httpContext.Request.Cookies[CookieName], out var existing))
        {
            return existing;
        }

        var id = Guid.NewGuid();
        httpContext.Response.Cookies.Append(CookieName, id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
        });

        return id;
    }
}
