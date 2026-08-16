using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Entities;

/// <summary>
/// Framework-coupled by design (derives from ASP.NET Core Identity's <see cref="IdentityUser{TKey}"/>),
/// which is why this lives in Identity.Infrastructure rather than Identity.Domain — Domain must
/// stay dependency-free, and Identity's actual authentication behavior is inherently an
/// infrastructure concern here (UserManager/SignInManager own the business rules, not us).
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
}
