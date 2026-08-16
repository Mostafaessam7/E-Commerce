using FluentAssertions;
using Identity.Application.Abstractions;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Security;

namespace IntegrationTests.Identity;

/// <summary>
/// Register -> confirm -> login, and forgot-password -> reset -> login, against the real
/// Identity tables — proves the token round-trip (generate/consume) actually works, not just that
/// each <see cref="IIdentityService"/> method compiles. <see cref="Store.Web.Controllers.AccountController"/>'s
/// URL-safe base64 encoding of the token isn't exercised here (that's an ASP.NET Core routing
/// concern, not an Identity one) - this tests the raw token IIdentityService hands back.
/// </summary>
public sealed class AccountFlowTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=ECommerce;Trusted_Connection=True;TrustServerCertificate=True";

    private ServiceProvider _provider = null!;
    private readonly List<string> _emailsToClean = [];

    public Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("ConnectionStrings:Database", ConnectionString)])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSecurityCore();
        services.AddIdentityModule(configuration);

        _provider = services.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        foreach (var email in _emailsToClean)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is not null)
            {
                db.Users.Remove(user);
            }
        }

        await db.SaveChangesAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Register_then_login_fails_until_the_email_is_confirmed_then_succeeds()
    {
        using var scope = _provider.CreateScope();
        SetFakeHttpContext(scope.ServiceProvider);
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var email = $"register-{Guid.NewGuid():N}@example.com";
        _emailsToClean.Add(email);

        var registerResult = await identityService.RegisterAsync(email, "Str0ng!Passw0rd");
        registerResult.IsSuccess.Should().BeTrue();

        var loginBeforeConfirm = await identityService.LoginAsync(email, "Str0ng!Passw0rd", rememberMe: false);
        loginBeforeConfirm.IsFailure.Should().BeTrue("RequireConfirmedEmail is on (docs/security.md) — an unconfirmed account must not be able to sign in");

        var tokenResult = await identityService.GenerateEmailConfirmationTokenAsync(registerResult.Value);
        tokenResult.IsSuccess.Should().BeTrue();

        var confirmResult = await identityService.ConfirmEmailAsync(registerResult.Value, tokenResult.Value);
        confirmResult.IsSuccess.Should().BeTrue();

        var loginAfterConfirm = await identityService.LoginAsync(email, "Str0ng!Passw0rd", rememberMe: false);
        loginAfterConfirm.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Forgot_password_token_resets_the_password_and_old_password_stops_working()
    {
        using var scope = _provider.CreateScope();
        SetFakeHttpContext(scope.ServiceProvider);
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var email = $"reset-{Guid.NewGuid():N}@example.com";
        _emailsToClean.Add(email);

        var registerResult = await identityService.RegisterAsync(email, "Original!Passw0rd");
        await identityService.ConfirmEmailAsync(
            registerResult.Value, (await identityService.GenerateEmailConfirmationTokenAsync(registerResult.Value)).Value);

        var resetTokenResult = await identityService.GeneratePasswordResetTokenAsync(email);
        resetTokenResult.IsSuccess.Should().BeTrue();

        var resetResult = await identityService.ResetPasswordAsync(email, resetTokenResult.Value, "NewStr0ng!Passw0rd");
        resetResult.IsSuccess.Should().BeTrue();

        var loginWithOldPassword = await identityService.LoginAsync(email, "Original!Passw0rd", rememberMe: false);
        loginWithOldPassword.IsFailure.Should().BeTrue();

        var loginWithNewPassword = await identityService.LoginAsync(email, "NewStr0ng!Passw0rd", rememberMe: false);
        loginWithNewPassword.IsSuccess.Should().BeTrue();
    }

    // SignInManager.PasswordSignInAsync writes the auth cookie through IHttpContextAccessor.HttpContext,
    // which is null outside a real ASP.NET Core request pipeline (this test composes a plain
    // ServiceCollection, no Kestrel — same pattern as every other IntegrationTests file). A bare
    // DefaultHttpContext with RequestServices wired to this scope is enough for SignInManager to
    // resolve what it needs and write the (here, discarded) cookie without throwing.
    private static void SetFakeHttpContext(IServiceProvider scopedProvider) =>
        scopedProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            RequestServices = scopedProvider,
        };
}
