Current Phase:
Phase 10 (Outbox processor), Phase 11 (Admin panel), and Phase 12 (Observability) complete. Next
up: whichever the user picks — remaining modules (Customers/Promotions/Shipping/Reviews/
Notifications), self-service Account UI (Register/ForgotPassword), or Docker/CI-CD.

Completed:
- Phase 1-6: Foundation, Persistence BB, Identity, Catalog, Ecomus storefront, Inventory.
- Phase 7+8: Ordering module — Cart + Order + Checkout, cross-module Contracts dispatch (ADR-014).
- Phase 9: Payments module — PaymentTransaction/RefundTransaction, FakePaymentGateway with real
  webhook signature verification + idempotency, reverse-direction ADR-014 (ADR-018).
  (See git log / prior entries for full detail on all of the above — condensed here.)
- Phase 10: Outbox processor. `Persistence.Outbox.OutboxProcessingService<TContext>` (generic,
  one instance per module context via `AddOutboxProcessor<TContext>()`) + `EventBus.InProcessEventBus`
  (in-process `IEventBus`, resolves `IIntegrationEventHandler<TEvent>` from DI). Wired into
  `Store.Worker` for `OrderingDbContext`/`PaymentsDbContext` (the two modules that currently
  enqueue events). Real bug found and fixed: `OutboxMessage.Type` needed to store
  `AssemblyQualifiedName`, not just `FullName` — a `ProjectReference` doesn't guarantee an
  assembly is JIT-loaded in the worker process; `Type.GetType(assemblyQualifiedName)` loads it
  if needed, a bare-FullName assembly scan silently can't (ADR-020). Verified against the real
  dev DB: all pending Ordering/Payments outbox rows processed with zero errors.
- Phase 11: Admin panel. `Store.Web/Areas/Admin` (not a new module) — Dashboard, Products
  (list/create/edit/publish/archive/delete/add-variant), Orders (list/detail/confirm/process/
  ship/deliver/cancel), Stock (list/adjust) — thin controllers dispatching new Application-layer
  commands/queries that are each a wrapper one step removed from an existing aggregate method
  (`Product.Publish()`, `Order.Cancel()`, `StockItem.AdjustTo()` — no new business rules).
  Permission-gated per action (`[Authorize(Policy = Permissions.X)]`, never role-name checks).
  Added `Store.Web/Controllers/AccountController.cs` (Login/Logout/AccessDenied) since the panel
  needed *something* to authenticate against — Identity's cookie already pointed `LoginPath` here
  from Phase 3, just had no controller yet. `Identity.Infrastructure.Seeding.AdminUserBootstrapper`:
  dev-only, opt-in (config-gated, User-Secrets-only) hosted service seeding one pre-confirmed
  admin user (ADR-021). Layout is a small hand-written stylesheet on top of the storefront's
  already-loaded Bootstrap bundle, deliberately not a curated `admin-ecomus` template integration
  (scope decision, see ADR-021) — revisit if visual polish is requested later.
  Verified end-to-end live in-browser: login → create product → add variant → publish →
  Dashboard/Orders/Stock pages all render real data.
- Phase 12: Observability. Serilog (Console + rolling daily file) replaces the default MEL
  console formatter in both `Store.Web` and `Store.Worker`, two-stage bootstrap-logger
  `try/catch/finally` pattern in both `Program.cs`. `CorrelationIdMiddleware`
  (`Store.Web.Infrastructure.Observability`) wraps every request in an `ILogger.BeginScope` with
  the correlation id — Serilog's MEL bridge turns that into a structured property automatically,
  so `GlobalExceptionHandler`'s own duplicate `BeginScope` was removed (ADR-022). Health checks:
  `GET /health` on Store.Web (`AddDbContextCheck<T>` per module context); Store.Worker (no
  inbound HTTP) runs the same checks on a 5-minute timer via a log-only `IHealthCheckPublisher`.
  Verified live: `/health` returns `Healthy`, request logs show `CorrelationId`/timing in both
  Console and `logs/store-web-*.log`.
  Real bug found: `tests/IntegrationTests/Outbox/OutboxProcessingServiceTests.cs` had a latent
  race — it asserted `OutboxMessage.ProcessedOnUtc` right after observing the test handler fire,
  but `MarkProcessed`+`SaveChangesAsync` happen slightly later in the same async chain. Went from
  "happens to pass" to consistently failing (unrelated EF Core patch bump from 10.0.0 to 10.0.11,
  needed for the new `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`
  package's own dependency floor, apparently shifted the timing enough to expose it). Fixed by
  polling the actual DB row instead of the in-memory handler list.
- All tests passing: 70 unit + 18 integration + 29 architecture.
- Commits: 71e7f96, 36008a1, c9f75b6, fd27d1f, bc563ff, d17f36d, 3b401e0 (Phase 1 through 10/11) —
  Phase 12 not yet committed as of this writing, see next actual commit hash in git log.

In Progress:
- (nothing — between phases)

Next:
- No Outbox handlers registered yet — events dispatch successfully but nothing reacts to them
  (no consumer module built: Notifications isn't started).
- No self-service Register/ForgotPassword UI — only Login/Logout exist; `IIdentityService`
  already has the methods, just no controller actions/views.
- Customers/Promotions/Shipping/Reviews/Notifications modules still have no Domain/Application
  code — placeholders only.
- Admin panel: no Brand/Category management UI (Product admin form omits BrandId/CategoryIds —
  scope decision, see ADR-021 area of docs/modules.md), no image upload (Section on file storage
  not started), no Payments admin UI (Payments has permissions defined — `Permissions.Payments.*`
  — but no admin controller yet).
- `admin-ecomus` template not integrated — current Admin UI is a minimal hand-styled layout.

Known Issues:
None outstanding.

Important Files:
- AGENTS.md — entry point; "EF Core gotchas" + "Other gotchas" sections, including the new
  AssemblyQualifiedName outbox gotcha and admin-panel authorization rule.
- docs/architecture.md, docs/modules.md — boundaries; Admin area described at the bottom of
  modules.md (it's not one of the 10 modules).
- docs/events.md — Outbox processor now real (Phase 10), not just documented intent.
- docs/security.md — Account controller, admin panel authorization, AdminUserBootstrapper
  credential handling (User Secrets only, never appsettings.json).
- docs/observability.md — Serilog, correlation id, health checks (Phase 12).
- docs/decisions.md — ADR-001..022.

Database Changes:
Local dev DB `ECommerce` (LocalDB), 5 migrated contexts unchanged from Phase 9 (Catalog,
Identity, Inventory, Ordering, Payments) — Phase 10/11 added no new tables/columns, only
Application-layer commands/queries over existing aggregates.

Decisions Made:
See docs/decisions.md. Newest: ADR-020 (generic per-module Outbox processor + in-process
EventBus, AssemblyQualifiedName fix), ADR-021 (Admin panel as a Store.Web Area, permission-gated,
dev-only AdminUserBootstrapper, no admin-ecomus template integration yet), ADR-022 (Serilog,
code-configured sinks, correlation-id middleware, per-module-context health checks).
