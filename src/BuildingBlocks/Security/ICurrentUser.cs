namespace Security;

/// <summary>
/// The authenticated caller, as every module's Application layer should ask for it — never by
/// reaching into <c>HttpContext.User</c> directly (that would leak an ASP.NET Core dependency
/// into Application/Domain code and make handlers untestable outside a web request). Backed by
/// <see cref="System.Security.Claims.ClaimsPrincipal"/> under the hood
/// (<c>HttpContextCurrentUser</c>), but callers only ever see this interface.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Null when <see cref="IsAuthenticated"/> is false.</summary>
    Guid? UserId { get; }

    string? Email { get; }

    bool IsInRole(string role);

    /// <summary>
    /// True when the current user holds the given permission (see <see cref="Permissions"/>).
    /// This is the check that matters for authorization decisions — roles are a convenient way to
    /// *grant* a bundle of permissions to a user (Phase 3), but code should never branch on role
    /// name directly; it should ask for the permission it actually needs.
    /// </summary>
    bool HasPermission(string permission);
}
