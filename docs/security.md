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

## Not yet built

Register/Login/ForgotPassword/ResetPassword UI + controllers (Account controller), 2FA, social
login, Admin authentication area — these consume `IIdentityService` when Store.Web gets its
Account controller (Phase 5+ territory, not yet scheduled).
