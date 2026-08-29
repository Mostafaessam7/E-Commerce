Current Phase:
Phase 10-29 complete — everything through Phase 25 (see below) plus a second gap-analysis pass
that found real issues: a misleading "Arabic+English localization" doc claim (fixed, no such
feature exists), a dead Wishlist link (removed), no rate limiting (Phase 26), no sitemap/robots.txt
(Phase 27), Customers never actually wired into checkout despite the module existing since Phase 17
(Phase 28, ADR-039 — `MergeCartCommand` had existed since Phase 7/8, registered in DI, but was
never once dispatched from anywhere until now), and no way for an admin to attach a product image
despite the storefront already rendering `PrimaryImageUrl` everywhere (Phase 29, ADR-040). Only two
item remains genuinely out of reach in this environment (a real `docker compose up --build` needs a
Docker daemon this sandbox can't run — genuinely attempted, Docker Desktop's backend never reaches
a ready state here) or are deliberate scope cuts recorded in their own ADRs (no Tax module, no
2FA/social login, no Wishlist module). Branch protection on `main` (require `build-and-test` to
pass before merge) is now live — the repo was made public and a `gh api` call applied it directly.

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
- Phase 30: Vanta.js animated backgrounds + Account-pages polish (ADR-041) — user-requested
  ("عايز UI UX احترافي, استخدم vanta.js"), scoped to Home's hero (`VANTA.NET`) and a shared
  split-panel visual on all four Account pages (`VANTA.WAVES`), not every screen — an animated
  background earns its cost on a landing hero/auth screen and actively hurts product grids/tables/
  checkout forms. `three.js`/Vanta self-hosted under `wwwroot/vendor/vanta/` (no CDN dependency,
  same discipline as `ecomus`/`admin-ecomus`), guarded behind `prefers-reduced-motion` + a real
  WebGL probe with a static fallback. Login/Register/ForgotPassword/ResetPassword rebuilt onto a
  shared `.auth-split` layout (new `wwwroot/css/site-custom.css`), buttons unified onto `tf-btn
  btn-fill radius-3`. Real bug found and fixed while auditing these pages: `_ValidationScriptsPartial`
  pointed at `~/lib/jquery-validation*` files that were never actually present in `wwwroot/lib` —
  client-side validation had been silently dead on Checkout (since Phase 7/8) and now these four
  pages the entire time, degrading every validation error to a full server round-trip. Fetched the
  real packages into `wwwroot/lib/`. Verified live: Home hero renders a real animated canvas behind
  the CTA text; Login/Register render the split panel with a live Vanta canvas; submitting Register
  empty now shows all three "field is required" messages instantly with no page reload. All 168
  tests still passing (view-markup-only change, no application code touched).
- Phase 31: Cart page real defects fixed while auditing for the same design-improvement ask
  (ADR-042). Two dead-code commands (`ApplyCouponCommand`/`RemoveCouponCommand` — existed since
  Phase 7/8, never registered in DI, same bug shape as `MergeCartCommand` before Phase 28 and the
  Phase 29 image commands) now actually wired to `CartController` + a real coupon input/remove UI
  on `Views/Cart/Index.cshtml` — coupon validation stays deferred to checkout's real
  `RedeemCouponCommand`, same "never trust the cart snapshot" rule as price/stock. Cart line items
  now show the real product image: `Catalog.Contracts.ProductVariantSnapshotDto` gained
  `PrimaryImageUrl`, `CartItem` gained a nullable `ImageUrl` column (new `AddCartItemImageUrl`
  migration, `ordering` schema) snapshotted once at add-to-cart time. Also fixed
  `Product/Details.cshtml`'s review-submission success/error banners, which had silently rendered
  as unstyled raw text the whole time Reviews has existed (Phase 20) — they used
  `admin-alert admin-alert-*`, a class only defined in the Admin-only stylesheet, never loaded on
  the storefront; switched to Bootstrap's `alert alert-success`/`alert alert-danger`. Added a real
  quantity input to the product page's add-to-cart form (previously hardcoded to 1). Verified
  live: uploaded a real product image, added it to cart as a genuine guest, confirmed the image
  renders in the cart row; applied a coupon and confirmed it persisted to `ordering.Carts` and
  removed cleanly; submitted a review and confirmed the success banner now has a real green
  background. All 168 tests still passing.
- Phase 32: Admin area status badges + Stock page fixed (ADR-043), auditing the Admin area for the
  same design-improvement ask. Every status pill across Orders/Payments/Products/Reviews had
  hardcoded the theme's green `block-available` class regardless of actual status — a Cancelled
  order looked identical to a Delivered one. New `Store.Web.Infrastructure.Admin.StatusBadge`
  centralizes the status-string → theme-class mapping (`block-available`/`block-pending`/
  `block-not-available`/`block-tracking`), applied across all 6 affected views. Stock page used to
  show only a raw `ProductVariantId` Guid per row; `SearchStockQueryHandler` now enriches each row
  with the real product name/SKU via `Catalog.Contracts.GetProductVariantSnapshotQuery` (ADR-014 —
  first time Inventory reads across the module boundary this way). Verified live post-login: a
  Pending order renders the orange `block-pending` class, an Active product renders the green
  `block-available` class, and the Stock page shows a real seeded product's name instead of its
  Guid. All 168 tests still passing.
- Phase 33: Payments-page order numbers + Checkout confirmation badges fixed (ADR-044), closing
  out the screen-by-screen design audit started in Phase 30. `ListPaymentsQueryHandler` now
  dispatches `GetOrderContactInfoQuery` per row (`Payments.Application` already referenced
  `Ordering.Contracts`) to show the real `OrderNumber` instead of a raw Guid link — same
  enrichment shape as Phase 32's Stock-page fix. `Views/Checkout/Confirmation.cshtml`'s
  Status/Payment badges had the identical "always the same color" bug as the Admin badges, just
  with Bootstrap classes; new `Store.Web.Infrastructure.Storefront.OrderStatusBadge` fixes it.
  Verified live end to end via a real guest checkout: placed a real order, confirmed the
  confirmation badges started `bg-warning` (Pending) and flipped to `bg-success` after the
  simulated payment (Confirmed/Paid), then confirmed that same order's real number rendered on the
  admin Payments page. Also cleaned up 14 long-orphaned `PaymentTransaction` rows found incidental
  to this verification (pre-existing dev-DB cruft, not a code defect). All 168 tests still passing.
- Phase 34: site-wide CSS class-name typos fixed (ADR-045) — the real substance behind live
  user feedback that "the design isn't right," found via a systematic audit (every class token
  used across `Views`/`Areas/Admin/Views` cross-referenced against every loaded stylesheet) rather
  than another one-off page read. Two defects, both predating this entire session: (1) 6 of the
  storefront's highest-traffic pages (Home, Shop, Product Details, Cart, Checkout, Checkout
  Confirmation) used `class="flat-spacing"`, which has zero CSS definition (`ecomus/css/styles.css`
  only defines `.flat-spacing-1` through `-5`) — every one of these pages has been rendering with
  zero section padding, content jammed against the header/footer, since the original Phase 5
  integration. (2) 11 admin list-row files used `class="item-row gap20"`, `item-row` likewise
  undefined — the theme's real row class is `.wg-product` (`display: flex`), so every admin list's
  columns had been block-stacked vertically instead of aligned in a row since Phase 11/21. Fixed
  both (`flat-spacing` → `flat-spacing-1`; `wg-product` added alongside `item-row`, which stays for
  a `main.js` remove-row selector). Verified live via computed styles, not a screenshot (no visual
  access in this environment): confirmed `flat-spacing-1` sections now compute real `70px`
  top/bottom padding (was `0px`), and a real admin row now computes `display: flex;
  justify-content: space-between` (was the browser's block-list default). All 168 tests still
  passing (Razor-view-markup-only change).
- Phase 35: continued the class-name audit into numeric-scale gaps (ADR-046). 12 admin buttons
  across 5 files used `w150`/`w100`, which don't exist (the theme ships `.tf-button.w128/.w180/
  .w208/.w230/.w380`) — every one of them was shrinking to its own text width instead of a
  consistent size, most visible on Reviews' "Pending"/"All" toggle pair rendering at two different
  widths. Replaced with the nearest real variants. Several views used `.mt-10`/`.mt-14`/`.mt-20`,
  which the theme's `.mt-*` scale doesn't reach (stops at `.mt-4`, unlike its richer `.mb-*` scale)
  — added the three missing values to `admin-overrides.css`. Also fixed a bug this session
  introduced itself: Phase 32's Stock-page fix used `.fs-14`, borrowed from the storefront theme
  without checking the admin theme has no font-size scale at all — added `.fs-14` there too.
  Verified live: confirmed via computed styles that `.w180` buttons now compute a real `180px`
  (was shrink-to-fit) and the Reviews toggle pair now matches. Hit a real environment quirk
  verifying the CSS-file changes specifically (the sandboxed preview browser cached the static
  `admin-overrides.css` file itself, unlike the always-fresh dynamically-rendered views) —
  confirmed the fix was actually correct by fetching the file through `curl` (a fully independent
  HTTP client, including through a real authenticated admin session), which got the exact
  up-to-date content every time. All 168 tests still passing.
- Phase 36: design-system foundation for a full premium storefront redesign (ADR-047), explicit
  user request. New `wwwroot/css/design-system.css` — CSS custom-property tokens (color, 8px
  spacing scale, two-tier radius, layered shadows, 220ms motion), additive-only on top of the
  curated `ecomus` theme (no markup renamed/restructured, so no Razor/JS/test surface changed).
  Re-themed: Header (sticky + solid white — also fixes a real Phase 30 bug, black nav text on the
  now-dark Vanta hero background was nearly invisible), Footer (dark/inverse tone), hero/section
  headings (`Young Serif` display accent), product cards (soft shadow + hover lift), buttons
  (consistent radius/motion). Verified live: header computes `position: sticky` + solid white +
  dark nav text; footer computes a dark background; headings compute the serif font stack; product
  cards compute the new radius/shadow tokens; no horizontal overflow at 375px mobile. All 168 tests
  still passing (purely additive CSS, no application code touched).
  - Small follow-up same phase: the hero `<h2>` itself was also invisible (near-black
    `styles.css` heading color winning over inherited `.text-white`) — a second real contrast bug
    caught by live user feedback right after the first push. Fixed with an explicit color in
    `design-system.css`; verified the computed color is `rgb(255,255,255)`.
- Phase 37: real homepage sections + real content pages (ADR-048), explicit user request ("add
  many pages and sections," scoped against that request's own "don't add unnecessary
  sections/features" rule by using only real existing data/destinations). `HomeController.Index`
  now builds a `HomeViewModel` — Featured (existing), New Arrivals, real active Categories, real
  active Brands — new "Shop by Category"/"Shop by Brand"/"New Arrivals" sections linking to Shop's
  already-existing (never previously linked to) `categoryId`/`brandId` filters. Nine footer/header
  `href="#"` links since Phase 5 are now real pages: About, Contact (real channels only, no fake
  contact-form submission — no backend exists to receive one), FAQ, Returns, Terms, a real Privacy
  Policy (was the literal untouched MVC-scaffold placeholder), and a data-backed Shipping page
  (dispatches `ListShippingMethodsQuery`, not hand-typed copy). While wiring the FAQ's order-
  tracking answer, found two real gaps and fixed both: no customer-facing order history existed at
  all despite `Order.CustomerId` being set since Phase 28 (added `CustomerId` to
  `OrderSearchCriteria`, a new `ProfileController.Orders` action + `Views/Profile/Orders.cshtml`);
  and `Checkout/Confirmation` had no ownership check — any order's full details were viewable by
  anyone holding its Guid regardless of who placed it (added `CustomerId` to `OrderDto`, the action
  now 404s a customer order that isn't the current signed-in user's; guest orders unaffected).
  Verified live end to end: placed a real order, confirmed it appeared correctly on the new My
  Orders page, confirmed via an anonymous `curl` request that the order's Confirmation URL now
  404s instead of leaking it; confirmed a category tile correctly filters Shop to the one real
  product in that category. All 168 tests still passing.
- Phase 38: redesign's last planned phase — Product Details, Shop filters, Cart, Checkout, Profile/
  My Orders, and the Phase 30 Auth pages all re-themed at once (ADR-049) by targeting the shared
  Bootstrap primitives they all already use (`.form-control`/`.form-select`, `.table`, `.alert`,
  `.badge`, `.pagination`) in `design-system.css`, rather than a page-by-page pass. Real bug hit
  verifying: `ecomus/css/styles.css`'s `input[type="text"], input[type="search"], ...` selector is
  more specific than a bare `.form-control` class, so the new input radius was silently losing
  despite loading last in the cascade — fixed with a scoped `!important`. Verified live via a real
  cart round-trip (added a product, confirmed the Cart table's new header/border treatment;
  confirmed the Shop search input now computes `8px` radius, not the theme's `3px`; confirmed
  Product Details' media container and heading font; no horizontal overflow at 375px mobile). All
  168 tests still passing. Every page named in the original redesign request has now been covered.
- Phase 39: real demo catalog data seeded (ADR-050), explicit user request. 4 brands, 6 categories,
  12 products (real descriptions, price, a real uploaded demo image each, 5 with a sale price, 5
  Featured) — all created through the app's own real admin HTTP endpoints, never raw SQL, so every
  domain invariant is enforced exactly as it would be for a real admin. The 3 leftover
  phase-verification products and "Phase 21 Verify" Brand/Category were archived/deactivated (not
  deleted) the same way. Building the Featured toggle required a real code fix first:
  `Product.Feature`/`Unfeature` existed in the domain since the original build but were never wired
  to any admin command — same dead-domain-method shape this session has now found four times
  (`MergeCartCommand`, the Phase 29 image commands, the Phase 31 coupon commands, this one). Real
  bug hit mid-seed: a `bash eval` with nested quoting silently mangled every `AddVariant` call in
  the first pass (the controller always redirects regardless of success/failure, so the HTTP status
  alone didn't reveal it) — caught only by checking the actual database state, not the redirect
  codes, then fixed and re-run cleanly. Verified live: Home's Category/Brand/Featured/New-Arrivals
  sections and Shop's listing all render the real seeded data with correct pricing; a product
  detail page's uploaded image loads for real (`naturalWidth: 720`). All 168 tests still passing.
- Phase 40: real storefront dark mode (ADR-051), explicit user request. `data-theme` attribute on
  `<html>`, set before first paint by a blocking script (reads `localStorage`, falls back to OS
  preference), toggled by a new header button, persisted across navigation. Overrides only the
  `design-system.css` `--ds-*` tokens for `:root[data-theme="dark"]` — deliberately never the
  theme's own `--main`/`--white` (used as text color in 223 places vs only 112 as background;
  inverting them would break every text usage). Every component `design-system.css` already
  governs (Phases 36-38) picks up dark mode automatically; a few base rules needed an explicit
  override. Admin's dark mode already existed (`admin-ecomus`'s own toggle since Phase 24) — not
  rebuilt. Verified live (in a fresh tab, after the sandboxed browser's known static-CSS-caching
  quirk gave a stale false negative in the original tab — see Phase 35/38): header/body/nav-text/
  product-card-shadow all compute correct dark values; toggle flips instantly and survives
  navigation; no horizontal overflow at 375px mobile. All 168 tests still passing.
- Phase 41: Arabic/English localization infrastructure + the entire core shopping flow (ADR-052),
  explicit user request. Standard ASP.NET Core `RequestLocalizationOptions` (`en`/`ar`), one shared
  `IStringLocalizer<SharedResource>` resource set (`Resources/SharedResource.ar.resx`) rather than
  per-view files, `LanguageController` writing the framework's own default culture cookie, a header
  language switcher, `<html lang dir>` set from the current culture, and a new scoped `rtl.css`
  (targets the components this project already owns — header, footer, hero, cards, tables, forms —
  not an exhaustive mirror of the curated theme's ~12,000 lines of CSS). Translated:
  Header/Footer/MobileMenu, Home, Shop, Product Details, Cart, Checkout + Confirmation. Real bug
  hit and fixed: `CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft` returns `false` for the
  neutral `"ar"` culture in this environment (confirmed live — `dir` stayed `"ltr"` even with every
  string correctly translated) — fixed with a direct `TwoLetterISOLanguageName == "ar"` check
  instead, verified by inspecting the raw HTTP response body once the property-based check's
  browser-side reading turned out unreliable. Deliberately not localized: catalog content
  (product/category/brand names are admin-entered data, not UI chrome) and domain-layer error
  messages. Verified live end to end via `get_page_text` (every string, not spot-checks): a real
  add-to-cart → checkout round-trip renders entirely in Arabic including localized number
  formatting; the language choice persists across navigation via the cookie. All 168 tests still
  passing (the E2E test client never sets a culture cookie, so it correctly exercises the English
  default throughout).
- Phase 42: Arabic/English localization continued (ADR-053) — Auth pages (Login/Register/
  ForgotPassword/ResetPassword + all six confirmation/status pages), Profile + My Orders, and all
  seven content pages (About/Contact/Faq/Returns/Terms/Shipping/Privacy). Opportunistic fix folded
  in: several Auth/Checkout ViewModels had no `[Display(Name=)]` attributes, so empty
  `<label asp-for="X">` tags were rendering raw property names (e.g. "RememberMe") — every such
  label now has explicit localized child content instead. `AccountController`/`HomeController` had
  no controller-owned hardcoded strings to localize; `ProfileController`'s four TempData messages
  now go through `IStringLocalizer` (domain `Error.Message` still left in English, per the Phase 41
  scoping decision). Reused existing resx keys wherever semantically identical (e.g. "My Orders",
  "Shipping", "Contact", "Returns + Exchanges") instead of duplicating; checked for duplicate
  `<data name>` keys before every commit. Verified live via `get_page_text` on every new page.
  All 168 tests still passing.
  Post-Phase-44 fix (found by a `/code-review` pass, not caught at the time): About.cshtml and
  Contact.cshtml were correctly wrapped in `@Localizer[...]` back in this phase, but 12 of their
  resx keys were never actually added — the duplicate-key checkpoint only ever caught keys that
  were wrongly *repeated*, not ones that were simply missing, so both pages silently rendered in
  English under Arabic until fixed. Added the missing keys, and while doing so hit the exact same
  case-collision shape as ADR-055's finding (`"Get in Touch"` vs `"Get in touch"` — this time
  self-inflicted while writing the fix) — caught immediately by the case-insensitive check and
  consolidated to one key before it ever shipped. Verified live via `get_page_text` on both pages.
- Phase 43: Arabic/English localization completed (ADR-054) — the entire Admin area: `_AdminLayout.cshtml`
  (sidebar, header language switcher, breadcrumb, footer, `dir`/`lang`/`rtl.css`), Dashboard, Products
  Index/Create/Edit, Brands, Categories, Coupons, Orders Index/Details, Payments, Stock,
  ShippingMethods, Reviews — every status badge value (`Active`/`Pending`/`Approved`/`Succeeded`/
  `Archived`/etc., the full `StatusBadge.CssClass` vocabulary) and every controller TempData message
  now render in the current language; domain `Error.Message` text still stays in English, same
  scoping as every prior localization phase. This closes "الموقع كله بما فيه لوحة الأدمن" — the whole
  site including the admin panel is now bilingual. Real bug hit and fixed: two resx keys differing
  only by case ("Short Description" vs "Short description", "Back to Sign In" vs "Back to sign in",
  "New Category" vs "New category") silently collided — one of each pair failed to resolve at
  runtime and rendered its English key text verbatim even on an otherwise fully-Arabic page, a
  genuine .NET resx/.resources case-sensitivity gap, not a caching artifact (confirmed via live
  `querySelector` DOM reads showing the same result as the raw HTTP response). Fixed by
  consolidating each pair to one key; added a case-insensitive duplicate check
  (`tr 'A-Z' 'a-z' | sort | uniq -d`) to the existing case-sensitive one before every resx commit
  going forward. Verified live via `get_page_text`/DOM reads across all ~18 admin pages while signed
  in as the seed admin account, including status-badge values, TempData success messages, and the
  language switcher round-trip back to English. All 168 tests still passing.

Next:
- All five originally-empty placeholder modules now have real code (Notifications: Phase 15,
  Customers: Phase 17, Promotions: Phase 18, Shipping: Phase 19, Reviews: Phase 20), both admin
  gaps named in the original analysis are closed (Phase 21), Redis has a real reader (Phase 22),
  CI publishes both images to GHCR (Phase 23), the Admin area uses the real admin-ecomus template
  (Phase 24), EndToEndTests proves the full journey works (Phase 25), rate limiting and a real
  sitemap exist (Phases 26-27), Customers is wired into checkout (Phase 28), admin product image
  upload is real (Phase 29), Home/Account pages have a real Vanta.js treatment (Phase 30), the
  Cart page has real product images + a working coupon UI (Phase 31), the Admin area's status
  badges/Stock page are fixed (Phase 32), the Payments page/Checkout confirmation badges are
  fixed (Phase 33), a site-wide CSS class-name audit found and fixed long-standing
  zero-padding/broken-flex/wrong-width/missing-margin defects across 20+ files (Phases 34-35), a
  full premium storefront redesign shipped on a new design-token foundation with dark mode
  (Phases 36-40), and Arabic/English localization now covers the entire site including the Admin
  area (Phases 41-43).
- Branch protection on `main` is now live (repo made public, then applied via `gh api PUT
  repos/.../branches/main/protection`: requires the `build-and-test` status check, strict/up-to-date
  branches, no force-pushes, no deletions). See docs/ci-cd.md for the exact request.

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
- `wwwroot/css/rtl.css` (Phase 41, re-audited Phase 44/ADR-055) is scoped to the components this
  app actually renders, cross-checked against every class its own `.cshtml` files reference — not
  a blind guess. The one real gap found and fixed was the admin sidebar/header/content-offset
  skeleton (`_AdminLayout.cshtml`); everything else candidate (product-card badges, mobile
  toolbar/offcanvas, `.sidebar-filter`) turned out either already symmetric or genuinely unused
  in this app's views. One property (`.main-content`'s `padding-left`/`padding-right` mirror)
  couldn't be confirmed via this session's browser tooling — `getComputedStyle` kept returning the
  old value even after an inline style override, which is impossible under a real CSS cascade, so
  it's logged as a tool-side stale-read artifact rather than a defect (source-level cascade
  order/specificity checked and correct) — worth a quick visual spot-check in a normal browser.

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
- docs/modules.md — "Storefront UI polish" section (Phase 30) covers Vanta.js + the auth-pages
  redesign, right after the Admin area section; Ordering section has the Phase 31 Cart UI note.
- docs/decisions.md — ADR-001..055.
- docs/modules.md — "Localization" section (Phases 41-43) covers the resource-based setup, right
  after "Dark mode".
- docs/modules.md — "Design system" section (Phase 36) covers the new token layer, right after
  "Storefront UI polish".

Database Changes:
Local dev DB `ECommerce` (LocalDB), 10 migrated contexts (Catalog, Identity, Inventory, Ordering,
Payments, Notifications, Customers, Promotions, Shipping, Reviews). Brand/Category tables, and
`Products.Images` / the `ProductImages` table Phase 29 now actually writes to, already existed in
the `catalog` schema since Phase 4 — no new migration needed for those. Phase 31 adds
`AddCartItemImageUrl` (`ordering` schema, `CartItems.ImageUrl` nullable column). Phases 32-34,
36-37 are application-code/Razor-view-only (no migration) — `Order.CustomerId`/every Category/Brand
column Phase 37 reads already existed; new query filters and DTO fields, no new columns. Redis
isn't a migrated `DbContext`. Phases 23-28, 30 were CI/workflow, Razor-views, new-test-project, and
application-code-only work respectively — no schema changes in those.

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
hand-written DI list, invisible to the build, only surfacing as a live 500 on first click), ADR-041
(Phase 30 adds self-hosted Vanta.js to Home's hero and the Account pages' split panel, scoped down
from "every screen" to the two places an animated background actually helps rather than hurts;
found and fixed a real, previously-invisible bug along the way — client-side validation had been
silently dead everywhere `_ValidationScriptsPartial` was used since Phase 7/8 because the vendor
files it referenced were never actually present in `wwwroot/lib`), ADR-042 (Phase 31 wires the
pre-existing but never-dispatched `ApplyCouponCommand`/`RemoveCouponCommand` into a real Cart-page
UI — same "exists but was never registered in DI" bug shape as `MergeCartCommand` and the Phase 29
image commands — and adds real product-image thumbnails to cart line items via a new
`PrimaryImageUrl` field on Catalog's cross-module variant snapshot; also fixed a storefront review
banner that had rendered as completely unstyled text since Phase 20 because it used an Admin-only
CSS class never loaded on the storefront), ADR-043 (Phase 32 fixes every Admin status pill —
Orders/Payments/Products/Reviews had all hardcoded the same green class regardless of actual
status — via a new centralized `StatusBadge` helper, and enriches the Stock page with real product
names/SKUs instead of a bare Guid via Inventory's first-ever cross-module read from Catalog),
ADR-044 (Phase 33 closes the design-audit series: the admin Payments page now shows a real
`OrderNumber` instead of a raw Guid via `Ordering.Contracts.GetOrderContactInfoQuery`, and the
Checkout confirmation page's badges get the same fix as Phase 32's Admin badges via a new,
storefront-specific `OrderStatusBadge` helper), ADR-045 (Phase 34 — the real substance behind live
"design isn't right" feedback: two site-wide CSS class typos predating this whole session,
`flat-spacing` and `item-row`, both with zero CSS definition — 6 storefront pages had zero section
padding and 11 admin list views had their row columns block-stacked instead of flex-aligned, found
via a systematic class-name audit rather than another one-off page read), ADR-046 (Phase 35
continues the same audit into numeric-scale gaps — `w150`/`w100` don't exist on 12 admin buttons,
`.mt-10`/`.mt-14`/`.mt-20` don't exist despite the theme's richer `.mb-*` scale, and `.fs-14` was a
bug this session introduced itself in Phase 32 by borrowing a storefront-only class), ADR-047
(Phase 36 starts a full premium storefront redesign, explicit user request — a new
`design-system.css` token layer, additive over the curated `ecomus` theme, applied so far to
Header/Footer/hero/headings/product-cards/buttons; fixed a real Phase 30 bug as a side effect —
the header's black nav text was invisible against the dark Vanta hero background it's floated over
since Phase 30), ADR-048 (Phase 37 adds real homepage sections — Category/Brand/New Arrivals, all
using pre-existing data never surfaced before — and turns nine dead footer/header links into real
content pages; found and fixed two real gaps while doing it, no customer order history existed
despite `Order.CustomerId` being set since Phase 28, and `Checkout/Confirmation` had no ownership
check at all — any order's details were viewable by anyone holding its Guid), ADR-049 (Phase 38,
the redesign's last planned phase — Product Details/Shop/Cart/Checkout/Profile/Auth pages re-themed
at once by targeting their shared Bootstrap primitives in `design-system.css`; hit and fixed a real
CSS-specificity bug, an element+attribute selector in the theme beat a plain class selector despite
loading later, so the new input radius was silently losing until given a scoped `!important`),
ADR-050 (Phase 39 seeds real demo catalog data — 4 brands, 6 categories, 12 products — through the
app's own real admin HTTP endpoints; required building `FeatureProductCommand`/`UnfeatureProductCommand`
first since `Product.Feature`/`Unfeature` had never been wired to any admin command, the fourth time
this session found that exact dead-domain-method shape), ADR-051 (Phase 40 adds a real storefront
dark mode via a `data-theme` attribute toggle, overriding only `design-system.css`'s own `--ds-*`
tokens rather than the theme's dual-purpose `--main`/`--white` variables, which would have broken
223 existing text-color usages), ADR-052 (Phase 41 begins real Arabic/English localization — one
shared `IStringLocalizer<SharedResource>` resource set, a header language switcher, RTL layout via
a scoped `rtl.css`; hit and fixed a real .NET/ICU gap where `TextInfo.IsRightToLeft` returns false
for the neutral "ar" culture, worked around with a direct language-code check — covering the
entire core shopping flow so far, Auth/Profile/content pages and Admin still to come), ADR-053
(Phase 42 continues the localization rollout — Auth pages, Profile/My Orders, and all seven content
pages; opportunistically fixed several ViewModels' missing `[Display(Name=)]` attributes while
localizing their labels — only the Admin area remains), ADR-054 (Phase 43 finishes the rollout —
the entire Admin area, ~18 files, every status badge value and controller TempData message; found
and fixed a real .NET resx case-sensitivity gap where two keys differing only by letter case
silently collided, one always losing at runtime).


---

## Phase 45 (2026-08-28): orphaned upload files, CI vulnerability gate, restore caching

Three items carried as known gaps were closed; a fourth was found while closing the first.

**Product image files are now actually deleted.** ADR-040 recorded "`RemoveImage` deletes the DB
row only, not the physical file" as an accepted simplification. Reviewing it found the gap was
wider than written — three orphan paths, not one:

1. `RemoveImage` — row deleted, file kept (the documented one).
2. `UploadImage` — the file is written to disk *before* `AddProductImageCommand` runs; if that
   command fails, the file stays with nothing referencing it. Never recorded anywhere.
3. `Delete` (the product itself) — takes every image row with it, leaving the entire per-product
   upload folder behind.

`IProductImageStorage` gained `Delete(url)` and `DeleteAllForProduct(productId)`; all three call
sites clean up. `RemoveProductImageCommand` returns the removed URL instead of `Unit` so the Web
layer knows which file to drop — Catalog still only ever handles a URL string, keeping the
ADR-040 seam intact. Deletion happens *after* the row is gone, preserving the property ADR-040
called out: an orphaned file is untidy, a row pointing at a missing file is a broken storefront
image.

`Delete` treats the URL as untrusted despite it coming from the database, requiring the resolved
path to sit under `wwwroot/uploads/products` before touching anything. New
`LocalProductImageStorageTests` (12 tests, no DB) covers this; the traversal cases were verified to
fail when the guard is removed, so they assert real protection rather than passing incidentally.

**CI now gates on vulnerable NuGet packages** (`docs/ci-cd.md`). The step inspects the command's
output rather than its exit code — `dotnet list package --vulnerable` exits 0 even when it finds
something, so simply running it would report and pass. Placed right after restore so a bad
dependency fails before the slow LocalDB/migrations/integration stretch. The tree is clean today,
so the gate went in green.

**Dependabot added** (`.github/dependabot.yml`) — weekly NuGet + github-actions, with Microsoft/
System and test-tooling grouped so a .NET release train is one PR rather than a dozen.

**NuGet restore caching enabled.** `docs/ci-cd.md` recorded this as blocked on a
`packages-lock.json` the repo doesn't generate. It wasn't blocked: `cache-dependency-path` accepts
any file to hash, and Central Package Management already puts every version in a single
`Directory.Packages.props` — a complete cache key on its own.

Verification: `dotnet build` clean, and the full suite run locally against LocalDB —
**102 unit + 29 architecture + 31 integration + 18 end-to-end = 180 passing, 0 failed**.

---

## Shared design system — Amber Commerce theme (2026-08-29)

The storefront now takes its colour from the workspace-shared design system in
`MeCodex/design-system` rather than defining it locally. 24 declarations across 12 `--ds-*` tokens
re-point at the shared `--mx-*` set; `design-system.css` keeps its own structure and every
component rule, so only the colour values moved.

Deliberately **not** changed: the purchased Ecomus theme and this storefront's type stack (Young
Serif / Albert Sans). The workspace theme decision covered colour. The display face is part of this
storefront's identity, and swapping it is a different change needing its own rationale.

Worth recording because it nearly went wrong silently: this app serves static files through
`MapStaticAssets`, which resolves a fingerprinted manifest baked at compile time. Files dropped
into `wwwroot` can 404 at runtime even though they exist on disk, if the build did not pick them
up. Verified against a running instance rather than assumed — `/design-system/tokens.css` and
`/design-system/themes/amber-commerce.css` both return 200 and the served file is the theme, not a
stale copy.

Every other product in the workspace has its own theme over the same token architecture; this one
is Amber Commerce. Token *names* are identical across all themes, so a component written against
`--mx-surface` works under any of them.
