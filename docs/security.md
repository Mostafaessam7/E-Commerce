# Security

## Foundation (`Security` building block, Phase 1)

- `ICurrentUser` — claims-backed current-user abstraction (`HttpContextCurrentUser`).
  Application/Domain code depends on this, never `HttpContext`/`ClaimsPrincipal` directly.
- `Permissions` — static catalog of every permission string, single source of truth for policy
  definitions and claim seeding.
- `CustomClaimTypes.Permission` — claim type carrying a permission string.

## Identity module (Phase 3)

- `Identity.Infrastructure`: `ApplicationUser`/`ApplicationRole` (`IdentityUser<Guid>`/
  `IdentityRole<Guid>`) — framework-coupled, so they live in Infrastructure, not Domain.
  `AppIdentityDbContext : IdentityDbContext<...>`.
- `Identity.Application.Abstractions.IIdentityService` — Register/Login/Logout/ConfirmEmail/
  ResetPassword, returning `Result`/`Result<T>`. Keeps `UserManager`/`SignInManager` out of
  Application (implemented by `IdentityService` in Infrastructure).
- Authorization: one policy per `Permissions.*` constant, registered in
  `Identity.Infrastructure/DependencyInjection.cs` (`AddAuthorization` loop over
  `Permissions.All`, `RequireClaim(CustomClaimTypes.Permission, permission)`).
  Use `[Authorize(Policy = Permissions.Orders.Cancel)]`, never role-name checks.
- `PermissionRoleSeeder` (`IHostedService`) — idempotently ensures an "Admin" role holding every
  permission claim. Does **not** create a default admin user/password — that's a deployment-time
  step, not startup code.
- Cookie auth: `services.ConfigureApplicationCookie(...)` in the same DI extension
  (`LoginPath=/Account/Login`, secure cookie, 14-day sliding expiration).
  `Store.Web/Program.cs` calls `app.UseAuthentication()` before `UseAuthorization()`.
- Password policy: 8+ chars, no non-alphanumeric requirement, unique email required, email
  confirmation required to sign in, lockout after 5 failed attempts (15 min).

## Not yet built

Register/Login/ForgotPassword/ResetPassword UI + controllers (Account controller), 2FA, social
login, Admin authentication area — these consume `IIdentityService` when Store.Web gets its
Account controller (Phase 5+ territory, not yet scheduled).
