# Security

Status: BuildingBlocks/Security foundation only (Phase 1). Identity module
implementation is Phase 3 — update this file when it lands.

## Foundation (Phase 1, done)

- `Security.ICurrentUser` — claims-backed current-user abstraction
  (`HttpContextCurrentUser`). Application/Domain code depends on this
  interface, never on `HttpContext`/`ClaimsPrincipal` directly.
- `Security.Permissions` — static catalog of every permission string
  (`Catalog.View`, `Orders.Cancel`, ... — full list in the source file).
  Single source of truth for both policy definitions and claim seeding.
- `Security.CustomClaimTypes.Permission` — the claim type carrying a
  permission string; a user's principal holds one such claim per permission
  they hold (flattened from roles at sign-in).

## Planned (Phase 3)

- ASP.NET Core Identity in `Identity` module: `ApplicationUser`, roles,
  register/login/logout/forgot-reset password/email confirmation/lockout.
- Authorization policies bound 1:1 to `Permissions.*` constants
  (`[Authorize(Policy = Permissions.Orders.Cancel)]`), not role-name checks.
- Admin authentication under `Areas/Admin`.
