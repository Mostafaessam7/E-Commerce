Current Phase:
Phase 10-29 complete — everything through Phase 25 (see below) plus a second gap-analysis pass
that found real issues: a misleading "Arabic+English localization" doc claim (fixed, no such
feature exists), a dead Wishlist link (removed), no rate limiting (Phase 26), no sitemap/robots.txt
(Phase 27), Customers never actually wired into checkout despite the module existing since Phase 17
(Phase 28, ADR-039 — `MergeCartCommand` had existed since Phase 7/8, registered in DI, but was
never once dispatched from anywhere until now), and no way for an admin to attach a product image
despite the storefront already rendering `PrimaryImageUrl` everywhere (Phase 29, ADR-040). Only two
items remain genuinely out of reach in this environment (branch protection needs a GitHub remote; a
real `docker compose up --build` needs a Docker daemon this sandbox can't run) or are deliberate
scope cuts recorded in their own ADRs (no Tax module, no 2FA/social login, no Wishlist module).

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
- Phase 22: real Redis-backed caching (ADR-033) — the Redis container provisioned since Phase 13
  gets its first reader. New `BuildingBlocks/Caching` project (`AddDistributedCaching` — real
  Redis via `AddStackExchangeRedisCache` when `ConnectionStrings:Redis` is configured, in-memory
  fallback otherwise). `Catalog.Infrastructure.Caching.CachedProductQueries` decorates
  `IProductQueries`, read-through caching the storefront's `GetBySlugAsync`/`SearchAsync`
  (TTL-only: 60s/30s, no write-side eviction). `GetVariantSnapshotAsync` (checkout's price/stock
  re-validation) and admin listings are deliberately never cached. `AddCatalogModule` also calls
  `AddDistributedMemoryCache()` as a `TryAdd` safety net so any composition that never calls
  `AddDistributedCaching` (every integration test) still resolves `IDistributedCache`. Tests: 6 new
  unit tests (`CachedProductQueries` — cache-hit avoids a second inner call, a miss is cached too,
  different search criteria don't collide, admin listings and variant snapshots are never cached).
  Verified against a **real local Redis instance** (not just the in-memory fallback): confirmed
  real `ecommerce:catalog:product:*` keys/TTLs appeared in Redis after visiting a product page and
  the shop listing; mutated the product's name directly in the DB and confirmed the *old* name
  kept being served until the cached entry's TTL expired (proving the cache was actually being
  read, not silently bypassed); confirmed the mutated name appeared automatically once the TTL
  lapsed with no manual invalidation.
- All tests passing: 102 unit + 30 integration + 29 architecture.
- Phase 23: CI image publish (ADR-034) — new `publish-images` job in
  `.github/workflows/build-test.yml` pushes `Store.Web`/`Store.Worker` images to GHCR
  (SHA + `latest` tags) on every push to `main`/`master`, gated on `build-and-test` passing,
  authenticated via the workflow's own `GITHUB_TOKEN`. Same commit fixed a real bug found while
  touching this file: `build-and-test`'s per-context `dotnet ef database update` list was 5 years
  — sorry, 5 *phases* — stale, missing every context Phases 15/17/18/19/20 added (Notifications/
  Customers/Promotions/Shipping/Reviews); IntegrationTests would have started failing the moment
  any test touched one of those tables, undetected only because no remote exists yet to actually
  trigger this workflow. Docker itself remains genuinely unrunnable in this sandbox — Docker
  Desktop was launched this session (not assumed unavailable from Phase 13's note) and its backend
  process was observed exiting within ~15s every time (no nested virtualization here). Verified
  everything short of that: `docker compose config` validates and interpolates `docker-compose.yml`
  correctly (including Phase 22's new `ConnectionStrings__Redis`) without needing a daemon; the
  workflow YAML parses correctly; and — the strongest available substitute for `docker build` —
  running the exact `dotnet restore`/`dotnet publish` commands each Dockerfile's `RUN` steps
  execute, against a byte-for-byte copy of the Dockerfiles' own build context, succeeded for both
  projects and produced the exact `Store.Web.dll`/`Store.Worker.dll` each `ENTRYPOINT` expects.
  Branch protection remains explicitly out of reach (needs a GitHub remote this repo doesn't have).
- All tests passing: 102 unit + 30 integration + 29 architecture (unchanged — CI/workflow-only phase).
- Phase 24: admin-ecomus template integration (ADR-035) — the Admin area's hand-styled placeholder
  chrome is replaced with a real `admin-ecomus` ThemeForest template integration, same curated-
  subset approach as the storefront's own Phase 5. Curated ~1.3MB asset subset (`css/`, a
  hand-picked `js/` — no `apexcharts`/`morris`/`raphael`/`jvectormap`, nothing here wires up
  charts/maps — `font/`, `icon/`) copied into `wwwroot/admin-ecomus/` out of the raw ~5MB/137-file
  package source. `_AdminLayout.cshtml` rebuilt on the template's real sidebar/header-dashboard/
  main-content structure (active-menu highlighting from `RouteData`, a real dark/light toggle,
  real signed-in user + sign out). All 18 view files across 10 admin controllers (Dashboard,
  Products, Brands, Categories, Orders, Payments, Stock, Coupons, ShippingMethods, Reviews)
  retargeted onto the template's own component classes (`wg-box`, `wg-table`/`item-row`,
  `form-style-1`, `tf-button`, status pills) instead of one-off bespoke markup per page. No fake
  demo content carried over (notification/inbox dropdowns with invented counts, chart trend
  arrows, stock photography) — only pieces backed by something real made it in. Verified live
  in-browser: signed in as the seeded admin, confirmed real computed CSS (fixed 320px sidebar,
  12px-radius cards, `icomoon` icon font resolving), confirmed every curated asset loaded 200 (not
  404), exercised a real Brand Deactivate/Activate round-trip and watched the themed alert +
  status pill flip live, viewed a real seeded order's detail page, confirmed the sidebar collapses
  off-canvas at a mobile viewport (the theme's own responsive behavior).
- All tests passing: 102 unit + 30 integration + 29 architecture (unchanged — Razor views only,
  no application code touched).
- Phase 25: EndToEndTests populated (ADR-036) — the last item in the user's confirmed "all 4
  categories, full scope" backlog. A real full-journey test (register → confirm email → log in →
  add to cart → check out → pay → order shows Paid) driven over **real HTTP** via
  `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory<Program>` — real MVC pipeline, real
  Razor-rendered forms, real antiforgery tokens scraped out of the actual HTML, real
  `NotificationLog`-sourced confirmation link, real cookie-based cart/auth identity. `Program.cs`
  gained a trailing `public partial class Program;` marker (standard, non-functional ASP.NET Core
  testing boilerplate) so the test project can reference it. Real bug hit and fixed while building
  this: the test client's `BaseAddress` must be `https://localhost`, not the default `http://` —
  both the cart-identity cookie and the auth cookie are `Secure`, so an `http://` client silently
  never sent them back, making every request look like a new anonymous visitor ("Cannot check out
  an empty cart"). One deliberate shortcut: payment initialization is dispatched directly via
  `IDispatcher` rather than through the real `POST /Payments/Pay` action, which makes its own
  outbound HTTP call back into the webhook endpoint — a self-referencing hop with no real socket
  inside an in-memory `TestServer`; the webhook call itself is still made for real, through the
  test `HttpClient`, against the real endpoint with a real signature. `.github/workflows/
  build-test.yml` runs it as an eighth `build-and-test` step. All 3 new tests passing.
- All tests passing: 102 unit + 30 integration + 29 architecture + 3 end-to-end.
- Docs accuracy pass: fixed `Directory.Build.props`/`docs/deployment.md`'s misleading
  "Arabic+English localization" claim (no translated UI, resource files, or RTL markup exist
  anywhere — the `InvariantGlobalization=false` flag only keeps ICU data available for if it's
  ever built) and removed `_Header.cshtml`'s dead Wishlist `href="#"` link (Wishlist was never one
  of the fixed 10 modules).
- Phase 26: rate limiting (ADR-037) — `Store.Web.Infrastructure.RateLimiting.RateLimiterExtensions`,
  per-IP fixed-window, `"auth"` (10/5min) on Login/Register/ForgotPassword/ResetPassword,
  `"webhook"` (30/min, more generous) on the payment webhook receiver. Verified with a real
  integration test driving 11 real login attempts through the real pipeline and asserting the
  11th gets a real 429.
- Phase 27: `sitemap.xml`/`robots.txt` (ADR-038) — `SeoController`, generated on every request
  from real, current Catalog data (`SearchProductsQuery`), not a static file that could go stale.
  Verified with real integration tests seeding an actual published product.
- Phase 28: Customers wired into checkout (ADR-039) — the follow-up ADR-028 explicitly deferred.
  `AccountController.Login` now calls `GetOrCreateCustomerCommand` (idempotent) and dispatches
  `MergeCartCommand` (existed since Phase 7/8, registered in DI, but never once dispatched from
  anywhere — a real gap, not new code) on every successful sign-in.
  `CartController`/`CheckoutController` resolve `CustomerId` from `ICurrentUser` instead of always
  `null`; checkout pre-fills from the customer's saved default address (informational only — the
  submitted form is still authoritative). `IIdentityService.LoginAsync` now returns
  `Task<Result<Guid>>` (mirroring `RegisterAsync`) instead of plain `Task<Result>`. Verified live
  in-browser end to end, including catching a real methodology bug along the way (a leftover admin
  session in the browser tab made the first "guest" cart attempt not actually a guest) — added to
  cart as a genuine guest, registered, confirmed via the real `NotificationLog` link, logged in,
  confirmed the cart merged, saved a default address, confirmed checkout pre-filled from it, placed
  the order, and confirmed in the database that `Order.CustomerId` matched the logged-in user's id
  exactly — the first order in this system's history to ever carry one.
- All tests passing: 102 unit + 30 integration + 29 architecture + 6 end-to-end (167 total).
- Phase 29: admin product image upload (ADR-040) — the last actionable gap from the "do everything
  missing" backlog. New `AddProductImageCommand`/`RemoveProductImageCommand` call the pre-existing
  (since Phase 4, never dispatched) `Product.AddImage` and a new `Product.RemoveImage` domain
  method (promotes the next image to primary when the removed one was primary). File writes are a
  `Store.Web`-only concern behind `IProductImageStorage`/`LocalProductImageStorage`
  (`wwwroot/uploads/products/{productId}/`, gitignored) — Catalog only ever sees a URL string.
  `Program.cs` gained a plain `app.UseStaticFiles()` alongside `MapStaticAssets()` since the latter
  only serves the build-time manifest, never files written at runtime. New Images panel on the
  admin Product Edit page (upload + thumbnail grid + Remove). New integration test proves
  add/remove round-trips through the real DB and promotes the next primary. Real bug caught during
  live verification, not by any test: the two new command handlers were never registered in
  `Catalog.Infrastructure/DependencyInjection.cs` (this module registers handlers by hand) — built
  clean, only surfaced as a live 500 on the first real click; fixed and re-verified. Verified live:
  uploaded a real PNG through the admin panel, confirmed it served back as `200 image/png` from the
  new static-file path and appeared as the thumbnail on both the product list and its own edit
  page, then removed it and confirmed the DB row and the physical file's URL both disappeared from
  the UI.
- All tests passing: 102 unit + 31 integration + 29 architecture + 6 end-to-end (168 total).

In Progress:
- None.

Next:
- All five originally-empty placeholder modules now have real code (Notifications: Phase 15,
  Customers: Phase 17, Promotions: Phase 18, Shipping: Phase 19, Reviews: Phase 20), both admin
  gaps named in the original analysis are closed (Phase 21), Redis has a real reader (Phase 22),
  CI publishes both images to GHCR (Phase 23), the Admin area uses the real admin-ecomus template
  (Phase 24), EndToEndTests proves the full journey works (Phase 25), rate limiting and a real
  sitemap exist (Phases 26-27), Customers is wired into checkout (Phase 28), and admin product
  image upload is real (Phase 29). No further actionable gaps are currently tracked.
- No branch protection rule requiring CI to pass before merge — that's a GitHub repo setting,
  genuinely out of reach until this repo has a remote (docs/ci-cd.md).

Known Issues:
- `docker compose up --build` still hasn't been run against a real Docker daemon — genuinely
  attempted in the Phase 23 session (not just carried over from Phase 13's note): Docker Desktop
  was launched, its backend process observed exiting within ~15 seconds every time (this sandbox
  has no nested virtualization). What *was* verified without a daemon: `docker compose config`
  (validates/interpolates `docker-compose.yml`, including Phase 22's `ConnectionStrings__Redis`),
  the `publish-images` workflow YAML parsing correctly, and — closest available substitute for
  `docker build` — the exact `dotnet restore`/`dotnet publish` commands each Dockerfile's `RUN`
  steps execute succeeding against a byte-for-byte copy of the Dockerfiles' own build context,
  producing the exact DLLs each `ENTRYPOINT` expects. Only the container-runtime layer itself is
  unverified. Worth a real `docker compose up --build` pass in an environment where Docker Desktop
  can actually start.

Important Files:
- AGENTS.md — entry point; "EF Core gotchas" + "Other gotchas" sections, including the new
  AssemblyQualifiedName outbox gotcha and admin-panel authorization rule.
- docs/architecture.md, docs/modules.md — boundaries; Admin area described at the bottom of
  modules.md (it's not one of the 10 modules).
- docs/events.md — Outbox processor now real (Phase 10), not just documented intent.
- docs/security.md — Account controller, admin panel authorization, AdminUserBootstrapper
  credential handling (User Secrets only, never appsettings.json).
- docs/observability.md — Serilog, correlation id, health checks (Phase 12).
- docs/deployment.md — Docker/docker-compose, migrations-in-container, Redis now really used (Phase 22).
- docs/ci-cd.md — GitHub Actions build+test + publish-images workflow (Phase 14, Phase 23, Phase 25).
- docs/testing.md — four test projects now, including the real-HTTP EndToEndTests (Phase 25).
- docs/security.md — rate limiting section added (Phase 26).
- docs/decisions.md — ADR-001..040.

Database Changes:
Local dev DB `ECommerce` (LocalDB), 10 migrated contexts (Catalog, Identity, Inventory, Ordering,
Payments, Notifications, Customers, Promotions, Shipping, Reviews) — unchanged since Phase 20
(Brand/Category tables, and `Products.Images` / the `ProductImages` table Phase 29 now actually
writes to, already existed in the `catalog` schema since Phase 4, no new migration needed; Redis
isn't a migrated `DbContext`; Phases 23-29 were CI/workflow, Razor-views, new-test-project, and
application-code-only work respectively — no schema changes anywhere in this range).

Decisions Made:
See docs/decisions.md. Newest: ADR-036 (Phase 25 populates `EndToEndTests` with a real
register-to-paid-order journey driven over real HTTP via `WebApplicationFactory<Program>`, not
`IntegrationTests`' plain `ServiceCollection` composition; hit and fixed a real bug along the way —
the test client needs an `https://` `BaseAddress` or the app's `Secure` cookies never round-trip),
ADR-037 (Phase 26's per-IP rate limiting — `"auth"` 10/5min on Login/Register/ForgotPassword/
ResetPassword, `"webhook"` 30/min on the payment receiver — sits in front of, not instead of,
Identity's per-account lockout), ADR-038 (Phase 27's `sitemap.xml`/`robots.txt` generated on every
request from real Catalog data, not a static file that could go stale), ADR-039 (Phase 28 wires
Customers into checkout — `AccountController.Login` now dispatches the `MergeCartCommand` that had
existed since Phase 7/8 but was never once called from anywhere; `CustomerId` flows through
`Cart`/`Checkout` instead of always `null`; verified live end to end down to the database row),
ADR-040 (Phase 29 adds admin product image upload — `Product.AddImage` finally gets dispatched, a
new `Product.RemoveImage` promotes the next primary, and the actual file write lives behind a new
`IProductImageStorage` seam in `Store.Web` so Catalog itself only ever deals in a URL string; hit
and fixed a real bug along the way — the two new command handlers weren't registered in Catalog's
hand-written DI list, invisible to the build, only surfacing as a live 500 on first click).
