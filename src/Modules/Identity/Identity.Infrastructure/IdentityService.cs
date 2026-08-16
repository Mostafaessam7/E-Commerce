using Identity.Application.Abstractions;
using Identity.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using SharedKernel.Results;

namespace Identity.Infrastructure;

internal sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public IdentityService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<Result<Guid>> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await _userManager.CreateAsync(user, password);

        return result.Succeeded
            ? Result.Success(user.Id)
            : Result.Failure<Guid>(ToValidationError(result));
    }

    public async Task<Result> LoginAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken = default)
    {
        var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return Result.Success();
        }

        if (result.IsLockedOut)
        {
            return Result.Failure(Error.Forbidden("Identity.LockedOut", "This account is temporarily locked out."));
        }

        return Result.Failure(Error.Unauthorized("Identity.InvalidCredentials", "Invalid email or password."));
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default) => await _signInManager.SignOutAsync();

    public async Task<Result> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure(Error.NotFound("Identity.UserNotFound", "User was not found."));
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);

        return result.Succeeded
            ? Result.Success()
            : Result.Failure(ToValidationError(result));
    }

    public async Task<Result<string>> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Don't reveal whether the email exists — same success shape either way at the
            // controller level; the caller decides what to do with a NotFound here.
            return Result.Failure<string>(Error.NotFound("Identity.UserNotFound", "User was not found."));
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        return Result.Success(token);
    }

    public async Task<Result> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("Identity.UserNotFound", "User was not found."));
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        return result.Succeeded
            ? Result.Success()
            : Result.Failure(ToValidationError(result));
    }

    private static Error ToValidationError(IdentityResult result)
    {
        var description = string.Join(" ", result.Errors.Select(e => e.Description));
        return Error.Validation("Identity.ValidationFailed", description);
    }
}
