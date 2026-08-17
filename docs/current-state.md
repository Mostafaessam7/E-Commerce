Current Phase:
Phase 10-21 complete (Outbox/Admin/Observability/Docker/CI-CD/Notifications/self-service Account
UI/Customers/Promotions+real discount wiring/Shipping+real shipping-cost wiring/Reviews+moderation/
Brand+Category admin+Payments admin UI). All five originally-empty placeholder modules now have
real code, and both named admin gaps from the original analysis are closed. In progress on a
broader backlog: Redis usage, CI image publish, docker compose verification, admin-ecomus
integration, EndToEndTests (user asked for "all of it"; working through it phase by phase,
committing after each).

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
- Phase 13: Docker + docker-compose. Multi-stage Dockerfiles for `Store.Web` (`aspnet` runtime
  image) and `Store.Worker` (plain `runtime` image, no inbound HTTP) — build context is the repo
  root (Central Package Management needs it), `dotnet restore <single csproj>` not the `.slnx`.
  `docker-compose.yml`: `sqlserver` (2022, health-checked), `redis` (provisioned, unused by app
  code yet — ADR-023), `store-web` (port 8080, `/health`), `store-worker`. `.env`/`.env.example`
  for the SA password (never hardcoded — real credential, same discipline as everywhere else).
  `Persistence.MigrationExtensions.MigrateWithRetryAsync<TContext>` + an opt-in
  `ApplyMigrationsOnStartup` config flag (Compose-only, local `dotnet run` never sets it) so the
  containers self-migrate against a freshly-started SQL Server instead of needing a manual step.
  Real bug found: two `CA1305` analyzer warnings surfaced only under `-c Release` (Serilog's
  `.WriteTo.Console`/`.WriteTo.File` calls lacking an explicit `IFormatProvider`) — invisible in
  the Debug builds used all through Phase 12, caught only when verifying the exact `dotnet
  publish -c Release` command the Dockerfiles run; fixed with `CultureInfo.InvariantCulture`.
  Verified: `docker compose config` parses cleanly, both projects' exact Dockerfile publish
  commands succeed with zero warnings, published output includes `wwwroot`. Could not run an
  actual `docker build`/`docker compose up` in this session — Docker Desktop is installed but not
  running in the sandbox; ask the user to verify the full stack locally.
- Phase 14: CI/CD. `.github/workflows/build-test.yml` — build+test on every PR/push to
  `main`/`master`. `windows-latest` (not `ubuntu-latest`): `IntegrationTests` hardcode LocalDB
  connection strings per test file rather than reading configuration, matching every dev
  machine's actual setup; `windows-latest` ships LocalDB preinstalled under the same instance
  name, so the suite runs completely unmodified instead of needing a parameterization refactor or
  a parallel SQL-Server-service-container test path that could drift from local dev (ADR-024).
  Steps: restore → build (Release) → Unit + Architecture tests (fail fast, no DB needed) → start
  LocalDB → install pinned `dotnet-ef` → apply all 5 contexts' migrations (same commands as
  docs/database.md) → Integration tests. No image-publish step (out of this phase's scope — see
  docs/ci-cd.md "Not yet built"). Verified locally: ran the exact `dotnet ef database update`
  command the workflow uses against the real dev DB (idempotent no-op, confirming the command
  syntax is correct) and the full local test suite.
- All tests passing (Phase 1-14): 70 unit + 18 integration + 29 architecture.
- Commits: 71e7f96, 36008a1, c9f75b6, fd27d1f, bc563ff, d17f36d, 3b401e0, 7f6e1eb, 29985ed, 3a8bd06
  (Phase 1 through 14), 0f2323c + bd0e35a (docs audit fixes).
- Phase 15: Notifications module + first real Outbox consumer. `Order.Email` added (ADR-025) —
  checkout now collects an email regardless of guest/authenticated (`CheckoutFormModel`/
  `PlaceOrderCommand`/`Order.Place`), threaded onto `OrderPlacedIntegrationEvent`. Notifications
  (ADR-026): `NotificationLog`, `INotificationSender`/`FakeEmailSender`,
  `OrderPlacedNotificationHandler` + `PaymentSucceededNotificationHandler` (the latter dispatches
  a new `Ordering.Contracts.GetOrderContactInfoQuery` since its triggering event has no email).
  Wired into `Store.Worker` (where handlers actually fire) and `Store.Web` (DbContext/health only).
  New migrations: `AddOrderEmail` (OrderingDbContext), `InitialCreate` (NotificationsDbContext).
  Tests: 70 unit + 20 integration (2 new — proves both handlers actually write a `NotificationLog`
  row, not just that they're registered) + 29 architecture, all passing.
- Phase 16: self-service Register/ForgotPassword/ResetPassword UI. `IIdentityService` gained
  `GenerateEmailConfirmationTokenAsync`. `Notifications.Contracts.SendEmailCommand` (ADR-027) — a
  *dispatchable* counterpart to Notifications' event-reactive handlers, for emails that must be
  sent synchronously (a confirmation link has to exist before the response renders — no prior
  event to react to). `AccountController` gained Register/ConfirmEmail/ForgotPassword/
  ResetPassword actions + views, round-tripping Identity's tokens through
  `WebEncoders.Base64UrlEncode`/`Decode` (they contain `+`/`/`/`=`, unsafe raw in a query string).
  Tests: `tests/IntegrationTests/Identity/AccountFlowTests.cs` (2 new — register→login-fails-
  until-confirmed→confirm→login-succeeds, and forgot-password→reset→old-password-fails/new-
  password-succeeds). Verified live in-browser end to end: registered a real user, pulled the
  actual confirmation link out of the `NotificationLog` row (no real email provider), followed it,
  confirmed, logged in successfully.
- All tests passing: 70 unit + 22 integration + 29 architecture.
- Phase 17: Customers module. `Customer` (aggregate root, `Id` == owning `ApplicationUser.Id`,
  ADR-028) + `CustomerAddress` (child, saved reusable address book — first address added or the
  one left after removing the default always auto-promotes to default, enforced in the
  aggregate). `Customers.Application.Profile`: `GetOrCreateCustomerCommand` (create-if-missing,
  same shape as Ordering's `GetOrCreateCartCommand`), `UpdateProfileCommand`, `AddAddressCommand`/
  `RemoveAddressCommand`/`SetDefaultAddressCommand`, `GetCustomerProfileQuery`.
  `Store.Web.Controllers.ProfileController` ([Authorize], any signed-in user) — new "My Account"
  page, header account icon now routes to it when authenticated (else `/Account/Login`). New
  migration: `InitialCreate` (`CustomersDbContext`). Not wired into checkout yet — `PlaceOrderCommand`
  still always passes `CustomerId: null` (guest-only); deliberate scope cut (ADR-028), not an
  oversight. Tests: 5 new unit tests (default-address promotion rules) + 1 integration test
  (GetOrCreate idempotency + full address lifecycle against the real DB). Verified live
  in-browser: loaded "My Account" as a signed-in user (profile auto-created), added a real
  address, confirmed it was auto-marked default.
- All tests passing: 75 unit + 23 integration + 29 architecture.
- Phase 18: Promotions module + real discount wiring. `Coupon` (aggregate root —
  `Redeem`/`ReleaseRedemption`/`Activate`/`Deactivate`); `Promotions.Contracts.RedeemCouponCommand`/
  `ReleaseCouponCommand` (ADR-014/029) mirror Inventory's `ReserveStockCommand`/`ReleaseStockCommand`
  compensation shape exactly. `PlaceOrderCommandHandler` now actually redeems `cart.CouponCode`
  against the real subtotal (never trusted at face value — same rule as price/stock) and releases
  it if anything later in the same checkout fails. New `Permissions.Promotions.View`/`Manage` +
  admin `CouponsController` (list/create/activate/deactivate). New migration: `InitialCreate`
  (`PromotionsDbContext`). Tests: 7 new unit tests (Coupon validation rules) + 2 new integration
  tests (real discount applied end-to-end through checkout; a coupon redeemed for an order that
  then fails to place is released, not burned). Verified live in-browser: created a real coupon
  through the admin panel, saw it listed correctly.
- All tests passing: 82 unit + 25 integration + 29 architecture.
- Phase 19: Shipping module + real shipping-cost wiring into checkout. `ShippingMethod` (aggregate
  root — `Create`/`UpdateCost`/`Activate`/`Deactivate`; no zone/region modeling, ADR-030).
  `Shipping.Contracts.ListShippingMethodsQuery`/`GetShippingMethodQuery` (ADR-014).
  `PlaceOrderCommand`'s old `ShippingCost: decimal` parameter is gone, replaced with
  `ShippingMethodId: Guid` — the handler dispatches `GetShippingMethodQuery` right after tax
  computation to get the authoritative cost (never trusted from the client, same rule as
  price/stock). Checkout's `GET /Checkout` now renders a real radio-button shipping-method picker
  fed by `ListShippingMethodsQuery()`, replacing the old hidden hardcoded `50m`. New
  `Permissions.Shipping.View`/`Manage` + admin `ShippingMethodsController` (list/create/activate/
  deactivate). New migration: `InitialCreate` (`ShippingDbContext`). Tests: 6 new unit tests
  (`ShippingMethod` validation rules) — the existing checkout/admin/notification/payment/coupon
  integration tests were all updated to seed a real `ShippingMethod` and pass its id instead of a
  raw decimal (5 files touched: `CheckoutFlowTests`, `AdminOperationsTests`,
  `NotificationHandlersTests`, `PaymentWebhookTests`, `CouponCheckoutTests`). Verified live
  in-browser: created a real shipping method through the admin panel, then placed a real order
  through checkout and confirmed the order's shipping cost/total matched the seeded method's real
  cost ($75.00), not a hardcoded number.
- All tests passing: 88 unit + 25 integration + 29 architecture.
- Phase 20: Reviews module + storefront/admin moderation flow — the fifth and last originally-empty
  placeholder module. `Review` (aggregate root — `Submit`/`Approve`/`Reject`; every review starts
  `Pending`, `Approve`/`Reject` are one-way transitions, `Review.NotPending` blocks re-moderating
  an already-decided review, ADR-031). No "verified purchase" check against Ordering — accepted
  from anyone. `Reviews.Contracts` stays empty (no other module calls into Reviews, same as
  `Customers.Contracts`). Storefront: `ProductController`'s product page now dispatches
  `GetProductReviewsQuery` (approved-only + average rating) and accepts `SubmitReviewCommand` (no
  login required, guest-friendly like checkout) through a real form. New
  `Permissions.Reviews.View`/`Moderate` + admin `ReviewsController` (pending/all listing,
  approve/reject). New migration: `InitialCreate` (`ReviewsDbContext`). Tests: 7 new unit tests
  (`Review` validation/transition rules) + 2 new integration tests (a submitted review is Pending
  and invisible to the storefront query until approved; a rejected review stays invisible and
  can't be re-moderated). Verified live in-browser: submitted a real review on a product page
  (hidden, pending), approved it through the admin panel, confirmed it then appeared on the
  storefront page with the correct average rating.
- All tests passing: 96 unit + 27 integration + 29 architecture.
- Phase 21: Brand/Category admin management + Payments admin UI (ADR-032) — the two admin gaps
  explicitly named in the original analysis, not a new module. `Catalog.Application.Brands`/
  `Categories` (list/create/activate/deactivate, same admin command shape as Promotions/Shipping);
  `Product.SetBrand`/`SetCategories` added so the Edit form can change either after creation. The
  Product Create/Edit admin form now dispatches `ListBrandsQuery`/`ListCategoriesQuery` to render
  a real Brand select + Category checkboxes instead of always sending `BrandId`/`CategoryIds` as
  `null`. New `Payments.Application.Payments.ListPaymentsQuery`/`IPaymentsQueries` (admin-wide or
  narrowed to one order) + admin `PaymentsController` that can trigger the pre-existing
  `RefundPaymentCommand` inline. No new permission categories — `Permissions.Catalog.*` and
  `Permissions.Payments.*` already existed since Phase 11, just unused until now. Tests: 2 new
  integration tests (Brand create/deactivate/reactivate visibility in the active-only list;
  Category deactivate/reactivate) + 1 new integration test (`ListPaymentsQuery` narrowed to one
  order vs. the admin-wide listing). Verified live in-browser: created a real Brand and Category
  through the admin panel, attached both to an existing product through the now-real Edit form
  picker, confirmed the selection persisted after reload; confirmed the Payments admin page
  renders correctly.
- All tests passing: 96 unit + 30 integration + 29 architecture.

In Progress:
- Working through the rest of the user's "do all 4" backlog (see Next, below) — Redis usage, CI
  image publish, docker compose verification, admin-ecomus integration, EndToEndTests.

Next:
- All five originally-empty placeholder modules now have real code (Notifications: Phase 15,
  Customers: Phase 17, Promotions: Phase 18, Shipping: Phase 19, Reviews: Phase 20), and both admin
  gaps named in the original analysis are closed (Phase 21).
- Customers isn't wired into checkout (`PlaceOrderCommand.CustomerId` always null) — a follow-up,
  see ADR-028.
- No product image upload (Section on file storage not started).
- `admin-ecomus` template not integrated — current Admin UI is a minimal hand-styled layout.
- CI runs build+test only — no image publish/registry push, no branch protection rule configured
  (that's a GitHub repo setting; enable it once this repo has a remote — docs/ci-cd.md).
- Redis container is provisioned in docker-compose.yml but no application code uses it yet.

Known Issues:
- Phase 13's `docker compose up`/`docker build` was not actually executed in the authoring
  session (Docker Desktop installed but not running in that sandbox) — verified via
  `docker compose config`, the exact `dotnet publish -c Release` commands the Dockerfiles run,
  and inspecting publish output, but not a real container run. Worth a first real
  `docker compose up --build` pass before relying on it.

Important Files:
- AGENTS.md — entry point; "EF Core gotchas" + "Other gotchas" sections, including the new
  AssemblyQualifiedName outbox gotcha and admin-panel authorization rule.
- docs/architecture.md, docs/modules.md — boundaries; Admin area described at the bottom of
  modules.md (it's not one of the 10 modules).
- docs/events.md — Outbox processor now real (Phase 10), not just documented intent.
- docs/security.md — Account controller, admin panel authorization, AdminUserBootstrapper
  credential handling (User Secrets only, never appsettings.json).
- docs/observability.md — Serilog, correlation id, health checks (Phase 12).
- docs/deployment.md — Docker/docker-compose, migrations-in-container, Redis provisioning (Phase 13).
- docs/ci-cd.md — GitHub Actions build+test workflow (Phase 14).
- docs/decisions.md — ADR-001..032.

Database Changes:
Local dev DB `ECommerce` (LocalDB), 10 migrated contexts (Catalog, Identity, Inventory, Ordering,
Payments, Notifications, Customers, Promotions, Shipping, Reviews) — unchanged by Phase 21 (Brand/
Category tables already existed in the `catalog` schema since Phase 4; no new migration needed).

Decisions Made:
See docs/decisions.md. Newest: ADR-030 (Shipping's `ShippingMethod` aggregate has no zone/region
modeling — every active method applies everywhere; `PlaceOrderCommand`'s `ShippingCost: decimal`
parameter is replaced with `ShippingMethodId: Guid`, looked up server-side via
`GetShippingMethodQuery`, closing out the last hardcoded number — the flat `50m` — in the checkout
write path), ADR-031 (Reviews' `Review` aggregate starts every submission `Pending`; only an admin
`Approve`/`Reject` — one-way, blocked by `Review.NotPending` from re-moderating — moves it out, and
the storefront query only ever returns `Approved` ones; no "verified purchase" check against
Ordering, a deliberate scope cut), ADR-032 (Phase 21 closed the Brand/Category admin UI and
Payments admin UI gaps named in the original analysis — both were "wire up what already exists"
work, not new modules; no new permission categories needed since Catalog/Payments permissions
already existed since Phase 11).
