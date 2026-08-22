using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Store.Web.Controllers;

/// <summary>
/// Phase 41 (ADR-052): sets the standard ASP.NET Core culture cookie
/// (<see cref="CookieRequestCultureProvider.DefaultCookieName"/>, already one of the default
/// <c>RequestLocalizationOptions</c> providers registered in Program.cs) and redirects back to
/// wherever the switcher was clicked from — no new cookie/provider wiring needed.
/// </summary>
public sealed class LanguageController : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
    }
}
