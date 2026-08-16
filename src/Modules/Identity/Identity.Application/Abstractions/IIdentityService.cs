using SharedKernel.Results;

namespace Identity.Application.Abstractions;

/// <summary>
/// What the rest of the system (Store.Web controllers, other modules if ever needed) is allowed
/// to know about authentication — never <c>UserManager&lt;T&gt;</c>/<c>SignInManager&lt;T&gt;</c>
/// directly, so Identity.Application stays free of ASP.NET Core Identity's own framework types.
/// Implemented in Identity.Infrastructure (<c>IdentityService</c>), which does the
/// UserManager/SignInManager wrapping.
/// </summary>
public interface IIdentityService
{
    Task<Result<Guid>> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<Result> LoginAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task<Result> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken = default);

    Task<Result<string>> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default);
}
