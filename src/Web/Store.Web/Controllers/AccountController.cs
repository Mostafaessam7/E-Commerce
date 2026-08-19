using System.Text;
using Customers.Application.Profile;
using Identity.Application.Abstractions;
using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Notifications.Contracts;
using Ordering.Application.Carts;
using Store.Web.Infrastructure;
using Store.Web.Infrastructure.RateLimiting;
using Store.Web.Models;

namespace Store.Web.Controllers;

/// <summary>
/// The site-wide login/logout/register/password-reset the cookie auth handler redirects to (see
/// <c>Identity.Infrastructure.DependencyInjection</c>'s <c>ConfigureApplicationCookie</c>) — not
/// Admin-specific; the Admin area is just the first thing that actually requires being signed in.
/// Thin: parses the form, calls <see cref="IIdentityService"/>, maps the Result to a redirect or a
/// re-rendered form with the error — no business logic here. Confirmation/reset links are sent by
/// dispatching <see cref="SendEmailCommand"/> into Notifications (ADR-014) — the same
/// cross-module call pattern every other controller uses, not a special case for auth email.
/// </summary>
[AllowAnonymous]
public sealed class AccountController : Controller
{
    private readonly IIdentityService _identityService;
    private readonly IDispatcher _dispatcher;

    public AccountController(IIdentityService identityService, IDispatcher dispatcher)
    {
        _identityService = identityService;
        _dispatcher = dispatcher;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimiterExtensions.AuthPolicy)]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _identityService.LoginAsync(model.Email, model.Password, model.RememberMe, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            return View(model);
        }

        // Phase 28 (ADR-028's deferred follow-up): ensure the Customer profile exists (Customer.Id
        // == the same Guid, so this is idempotent — GetOrCreateCustomerCommand only creates it the
        // first time) and fold whatever the guest added to their cart before logging in into the
        // customer's own cart (MergeCartCommand — no domain failure path, always succeeds; an
        // unhandled exception here is a real bug, not something to swallow).
        var customerId = result.Value;
        await _dispatcher.Send(new GetOrCreateCustomerCommand(customerId, model.Email), cancellationToken);
        var anonymousId = HttpContext.GetOrSetAnonymousId();
        await _dispatcher.Send(new MergeCartCommand(customerId, anonymousId), cancellationToken);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _identityService.LogoutAsync(cancellationToken);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimiterExtensions.AuthPolicy)]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var registerResult = await _identityService.RegisterAsync(model.Email, model.Password, cancellationToken);
        if (registerResult.IsFailure)
        {
            ModelState.AddModelError(string.Empty, registerResult.Error.Message);
            return View(model);
        }

        var tokenResult = await _identityService.GenerateEmailConfirmationTokenAsync(registerResult.Value, cancellationToken);
        if (tokenResult.IsSuccess)
        {
            var confirmationLink = Url.Action(
                nameof(ConfirmEmail), "Account",
                new { userId = registerResult.Value, token = EncodeToken(tokenResult.Value) },
                protocol: Request.Scheme)!;

            await _dispatcher.Send(
                new SendEmailCommand(model.Email, "Confirm your email", $"Welcome! Confirm your account: {confirmationLink}"),
                cancellationToken);
        }

        return View("RegisterConfirmation");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(Guid userId, string token, CancellationToken cancellationToken)
    {
        var result = await _identityService.ConfirmEmailAsync(userId, DecodeToken(token), cancellationToken);
        return View(result.IsSuccess ? "ConfirmEmailSuccess" : "ConfirmEmailFailure");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimiterExtensions.AuthPolicy)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var tokenResult = await _identityService.GeneratePasswordResetTokenAsync(model.Email, cancellationToken);
        if (tokenResult.IsSuccess)
        {
            var resetLink = Url.Action(
                nameof(ResetPassword), "Account",
                new { email = model.Email, token = EncodeToken(tokenResult.Value) },
                protocol: Request.Scheme)!;

            await _dispatcher.Send(
                new SendEmailCommand(model.Email, "Reset your password", $"Reset your password here: {resetLink}"),
                cancellationToken);
        }

        // Same view either way — never reveal whether an email address has an account
        // (IIdentityService.GeneratePasswordResetTokenAsync's own doc comment makes the same call).
        return View("ForgotPasswordConfirmation");
    }

    [HttpGet]
    public IActionResult ResetPassword(string email, string token) =>
        View(new ResetPasswordViewModel { Email = email, Token = token });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimiterExtensions.AuthPolicy)]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _identityService.ResetPasswordAsync(model.Email, DecodeToken(model.Token), model.NewPassword, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            return View(model);
        }

        return View("ResetPasswordConfirmation");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ASP.NET Core Identity tokens contain +, /, = — unsafe unencoded in a query string (some of
    // those get silently mangled by proxies/browsers) — the standard fix is round-tripping through
    // WebEncoders' URL-safe base64, not raw Uri.Escape (which doesn't survive every proxy either).
    private static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string DecodeToken(string encoded) => Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
}
