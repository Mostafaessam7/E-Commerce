Current Phase:
Phase 10 (Outbox processor) and Phase 11 (Admin panel) complete. Next up: whichever the user
picks — remaining modules (Customers/Promotions/Shipping/Reviews/Notifications), self-service
Account UI (Register/ForgotPassword), or observability/CI-CD (later master-plan phases).

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
- All tests passing: 70 unit + 18 integration (5 new: product lifecycle, admin search status
  filter, order status walk, order cancel + admin search, stock adjust) + 29 architecture.
- Commits: 71e7f96, 36008a1, c9f75b6, fd27d1f, bc563ff (Phase 1 through 7/8), Phase 9's commit —
  Phase 10/11 not yet committed as of this writing, see next actual commit hash in git log.

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
- docs/decisions.md — ADR-001..021.

Database Changes:
Local dev DB `ECommerce` (LocalDB), 5 migrated contexts unchanged from Phase 9 (Catalog,
Identity, Inventory, Ordering, Payments) — Phase 10/11 added no new tables/columns, only
Application-layer commands/queries over existing aggregates.

Decisions Made:
See docs/decisions.md. Newest: ADR-020 (generic per-module Outbox processor + in-process
EventBus, AssemblyQualifiedName fix), ADR-021 (Admin panel as a Store.Web Area, permission-gated,
dev-only AdminUserBootstrapper, no admin-ecomus template integration yet).
