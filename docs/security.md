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

## Payment webhook security (Phase 9)

- `POST /api/webhooks/payments/{provider}` (`WebhooksController`) verifies an HMAC-SHA256
  signature (`X-Payment-Signature` header) over the raw request body before touching anything,
  using `CryptographicOperations.FixedTimeEquals` (not `==`) to avoid a timing side-channel on the
  comparison. An invalid signature returns 401 and nothing else runs.
- The signing secret (`Payments:WebhookSecret` in config) is a **dev-only fake value** — real
  webhook secrets from an actual provider (Stripe/Paymob/etc.) would need to move to a proper
  secret store (User Secrets locally, Key Vault/env var in production), not `appsettings.json`, the
  moment a real `IPaymentGateway` implementation replaces `FakePaymentGateway` (ADR-017).
- Idempotency: `ProcessedWebhookEvent` ledger (unique index on Provider+ProviderEventId) rejects
  reprocessing a duplicate delivery *before* any domain state changes — webhook providers retry on
  timeout, so a handler that isn't idempotent will double-apply a payment. `PaymentTransaction`'s
  guarded transitions (`MarkSucceeded`/`MarkFailed` no-op with a `Result.Failure` once already
  resolved) are defense-in-depth on top of the ledger, not a replacement for it.

## Account controller + Admin panel authorization (Phase 11)

- `Store.Web/Controllers/AccountController.cs`: `[AllowAnonymous]`, Login/Logout/AccessDenied only
  (no Register/ForgotPassword UI yet — not needed until self-service customer accounts are
  scheduled). `Login` calls `IIdentityService.LoginAsync`; the cookie's `LoginPath`/`AccessDeniedPath`
  (Identity's `DependencyInjection.cs`) already point here, so any `[Authorize]`-gated page redirects
  to it automatically, `returnUrl` and all.
- Admin area (`Areas/Admin`) controllers are gated with `[Authorize(Policy = Permissions.X)]` per
  action, never `[Authorize(Roles = "Admin")]` — same rule as everywhere else in this codebase; the
  "Admin" role is just the one role `PermissionRoleSeeder` happens to grant every permission to,
  not something authorization code checks by name.
- `Identity.Infrastructure.Seeding.AdminUserBootstrapper` (ADR-021): dev-only, opt-in hosted
  service. Reads `Identity:DefaultAdmin:Email`/`Password` from configuration; does nothing if
  either is unset. **These are real login credentials — set them via `dotnet user-secrets` (or an
  environment variable in a real deployment), never in `appsettings.json`**, unlike Payments'
  `WebhookSecret` (that one really is a fake value safe to commit; this one is not). Local dev:
  ```bash
  dotnet user-secrets set "Identity:DefaultAdmin:Email" "admin@example.com" --project src/Web/Store.Web
  dotnet user-secrets set "Identity:DefaultAdmin:Password" "<a-real-password>" --project src/Web/Store.Web
  ```

## Self-service Register/ForgotPassword/ResetPassword (Phase 16)

- `AccountController.Register`: calls `IIdentityService.RegisterAsync`, then
  `GenerateEmailConfirmationTokenAsync` (new — Phase 16), builds a `ConfirmEmail` link, and
  dispatches `Notifications.Contracts.SendEmailCommand` (ADR-014/027) to actually send it.
  `RequireConfirmedEmail = true` (Phase 3 policy) means the account can't sign in until that link
  is clicked — proven end-to-end by `tests/IntegrationTests/Identity/AccountFlowTests.cs` and live
  in-browser (register → real confirmation link from the `NotificationLog` row → confirm → login).
- `AccountController.ForgotPassword`/`ResetPassword`: same shape, using
  `GeneratePasswordResetTokenAsync`/`ResetPasswordAsync`. `ForgotPassword` always renders the same
  confirmation view regardless of whether the email exists — never reveals account existence
  (`IIdentityService.GeneratePasswordResetTokenAsync`'s own doc comment already established this;
  the controller just doesn't undo it).
- Identity token gotcha: ASP.NET Core Identity's tokens contain `+`/`/`/`=` — unsafe raw in a
  query string. `AccountController` round-trips every token through
  `WebEncoders.Base64UrlEncode`/`Base64UrlDecode` before/after putting it in a URL.

## Not yet built

2FA, social login.
