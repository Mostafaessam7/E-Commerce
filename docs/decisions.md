# Architecture Decision Log

Don't re-litigate an Accepted decision unless a new requirement forces it.

---
**ADR-001**
Decision: Modular Monolith (not microservices).
Reason: No independent-scaling or independent-deployment requirement exists
yet; a monolith with enforced module boundaries gets DDD/separation benefits
without distributed-systems operational cost. Revisit only if a real scaling
need appears for a specific module.
Status: Accepted.

---
**ADR-002**
Decision: `Result<T>` for expected/business failures, custom exceptions only
for unreachable-invariant failures; both map to identical ProblemDetails.
Reason: Keeps Application/Domain control flow explicit and testable without
try/catch everywhere, while still giving deep call stacks (interceptors,
background jobs) a way to fail that a Result can't reach.
Status: Accepted.

---
**ADR-003**
Decision: Module composition via explicit `Add{Module}Module()` calls in
`Program.cs`, not a reflection-discovered `IModule` interface.
Reason: The module list is closed and known at compile time (10 fixed
modules, not a plugin host). Explicit calls are the architecture diagram —
readable top to bottom — and fail at compile time, not silently at runtime.
Full rationale: `docs/module-composition.md` (superseded by this ADR as the
canonical record; keep the longer file as background reading).
Status: Accepted.

---
**ADR-004**
Decision: No MediatR; if CQRS mediation is needed, build a minimal in-house
dispatcher in a BuildingBlock.
Reason: MediatR's current versions require a commercial license; a project
this size doesn't need its full feature set.
Status: Accepted. Not yet implemented (add when the first CQRS handler
actually needs a pipeline).

---
**ADR-005**
Decision: One `DbContext` per module, not one shared context.
Reason: Enforces "no cross-module table access" at the type-system level and
keeps modules independently migratable.
Status: Accepted (Phase 2).

---
**ADR-006**
Decision: Optimistic concurrency tokens are an EF Core mapping concern
(shadow rowversion property), not a Domain model property.
Reason: Keeps a storage detail out of business logic; `AggregateRoot<TId>`
stays persistence-agnostic.
Status: Accepted (applied starting Phase 2/6, Inventory first).

---
**ADR-007**
Decision: Solution uses the `.slnx` format, not classic `.sln`.
Reason: .NET 10 SDK's `dotnet new sln` default; `dotnet sln`/`dotnet build`
both support it natively.
Status: Accepted.

---
**ADR-008**
Decision: Added a 6th BuildingBlock, `Persistence`, holding EF Core-dependent
shared code (`AppDbContextBase`, `OutboxMessage`, `AuditingInterceptor`,
soft-delete filter). Referenced only by `*.Infrastructure` projects, never
`*.Application`.
Reason: The original 5-building-block list (SharedKernel/EventBus/
Observability/Security/Infrastructure) had nowhere to put shared EF Core code
without either (a) adding an EF Core PackageReference to the existing
`Infrastructure` BB — which `*.Application` also references for
`IDateTimeProvider`, transitively leaking EF Core into every module's
Application layer and violating the "Application must stay EF Core-free" rule
(`TypeDependencyTests`) — or (b) duplicating ~150 lines of interceptor/outbox
boilerplate across all 10 modules. A dedicated building block avoids both.
Status: Accepted.

---
**ADR-009**
Decision: `ApplicationUser`/`ApplicationRole` (ASP.NET Core Identity types)
live in `Identity.Infrastructure`, not `Identity.Domain`. `Identity.Application`
defines `IIdentityService` instead of exposing `UserManager`/`SignInManager`.
Reason: `IdentityUser<TKey>` is a framework type; Domain must stay
dependency-free (same rule as every other module). Auth flows here are
inherently an infrastructure concern (UserManager owns the actual rules), so
Identity.Domain is intentionally near-empty for now.
Status: Accepted (Phase 3).

---
**ADR-010**
Decision: Added a 7th BuildingBlock, `Messaging` — minimal in-house CQRS
dispatcher (`IRequest<T>`/`ICommand<T>`/`IQuery<T>`/`IRequestHandler<T,R>`/
`IDispatcher`). Referenced by `*.Application` only. Handlers registered
explicitly per module (`services.AddScoped<IRequestHandler<Cmd,R>, Handler>()`),
no assembly scanning — consistent with ADR-003/004.
Reason: ADR-004 flagged this as "add when the first CQRS handler needs it" —
Catalog's `CreateProductCommand`/`GetProductBySlugQuery` were that trigger.
Status: Accepted (Phase 4).

---
**ADR-011**
Decision: Every module's DbContext gets its own SQL schema
(`AppDbContextBase.SchemaName`, or a direct `HasDefaultSchema(...)` call for
contexts that can't derive from it, like `AppIdentityDbContext`).
Reason: All modules share one physical database/connection string; without
per-module schemas, same-named tables across modules collide in the default
`dbo` schema — hit for real when Inventory's `OutboxMessages` table (every
`AppDbContextBase`-derived context gets one) collided with Catalog's.
Status: Accepted (Phase 6). See docs/database.md.

---
**ADR-012**
Decision: Every entity's `Guid Id` (assigned client-side via `Guid.NewGuid()`
in the constructor, which is 100% of entities in this codebase) is mapped
`ValueGenerated.Never`, applied automatically for every module via
`AppDbContextBase.OnModelCreating` → `MarkDomainAssignedGuidKeysAsNeverGenerated()`.
Reason: left at EF Core's default (`ValueGeneratedOnAdd` by convention for
Guid keys), a *new* child entity added to an *already-tracked* (loaded, not
just-constructed) parent's collection gets misclassified as `Modified` instead
of `Added` — EF's heuristic assumes a non-default key on a newly-discovered
entity means it already exists. Reproduced for real: `StockItem.Reserve()`
adding a new `StockTransaction` to a loaded `StockItem` generated an UPDATE
instead of an INSERT, failing with a spurious concurrency exception on the
very first save (no actual concurrent writer). This is systemic — it would
eventually bite any module doing the same "load aggregate, then add a new
child" pattern — so the fix is applied once, for every entity, at the base.
Status: Accepted (Phase 6). See docs/database.md.

---
**ADR-013**
Decision: `SharedKernel.ValueObjects.ValueObject` does not overload `==`/`!=`.
Reason: comparing a value-converted EF property (e.g. `Product.Slug`) against
a same-type instance via `==` must produce an `Expression.Equal` node for EF
Core to translate the comparison to SQL (applying the converter to both
sides); a custom `==` operator compiles to a method call instead, which EF
cannot translate and throws "could not be translated". `.Equals(...)` is
unaffected and remains the way to compare value objects in C# code.
Status: Accepted (Phase 4).

---
**ADR-014**
Decision: commands/queries meant for cross-module dispatch (another module
calling through the shared `IDispatcher`, not just Store.Web) live in the
*publishing* module's `*.Contracts` project, not its `*.Application` project.
The handler implementation still lives in `*.Application`/`*.Infrastructure`
as usual. `Contracts` may now reference `Messaging` (for `ICommand<T>`/
`IQuery<T>`), and `*.Application` may reference *any* module's `*.Contracts`
(ArchitectureTests updated accordingly) — but never another module's Domain/
Application/Infrastructure directly.
Reason: Ordering's checkout needs synchronous, real-time reads from Catalog
(current price/availability) and a synchronous write to Inventory (reserve
stock) — the Outbox/integration-events path is fire-and-forget/eventually
consistent, wrong for "does this exist and is there stock, right now, before
I place this order". `Contracts` was already the sanctioned "public surface"
a module exposes to others (docs/architecture.md, Phase 1); this just extends
that surface to include dispatchable requests, not only DTOs/events. Moved as
the first real instance: `ReserveStockCommand`/`ReleaseStockCommand`
(Inventory.Contracts) and `GetProductVariantSnapshotQuery`
(Catalog.Contracts). `Messaging.Unit` (no-return-value marker) also moved
from Inventory.Application to Messaging itself once a second module needed it.
Status: Accepted (Phase 7/8).

---
**ADR-015**
Decision: a module's Application-layer command/query namespace must never be
named exactly after one of that module's own Domain entity types (e.g. not
`Ordering.Application.Cart` when `Ordering.Domain.Cart` exists) — use a
distinguishing form instead (`Ordering.Application.Carts`, plural).
Reason: C# resolves an unqualified identifier against enclosing/sibling
namespaces before it falls back to `using`-imported types. A file in
`Ordering.Application.Abstractions` referencing `Cart` (intending
`Ordering.Domain.Cart` via `using Ordering.Domain;`) instead silently bound to
the sibling namespace `Ordering.Application.Cart`, failing with "'Cart' is a
namespace but is used like a type". Not a style preference — the original
name is a real compile break waiting to happen the moment such a file is
touched from a different namespace context.
Status: Accepted (Phase 7/8).

---
**ADR-016**
Decision: checkout tax is a flat placeholder rate (14%, a constant in
`PlaceOrderCommandHandler`), not a configurable or rule-based calculation.
Reason: no Tax module/config exists among the fixed 10 modules and Section 4
only mentions "VAT/Tax configuration" in passing. A real implementation needs
a real requirement (per-category rates? per-region?) that hasn't been given
yet — a flat constant is honest about being a placeholder and easy to find/
replace later, versus building a rate-lookup abstraction speculatively.
Status: Accepted (Phase 7/8) — revisit if/when tax rules are specified.

---
**ADR-017**
Decision: Payments has no real payment provider integration — `FakePaymentGateway`
is a same-process fake behind `IPaymentGateway`, but it exercises the *real*
mechanics Section 9 asks for (HMAC-SHA256 signed webhook payloads, real
signature verification, idempotent processing via a `ProcessedWebhookEvent`
ledger) rather than short-circuiting them. A "Simulate payment" button in
Store.Web signs a payload with `IWebhookSimulator` and POSTs it through the
real `/api/webhooks/payments/{provider}` endpoint — the same code path a real
provider's callback would hit.
Reason: no Stripe/Paymob/Checkout.com account exists for this project. Faking
the whole flow (e.g. directly flipping PaymentStatus from a button) would
leave Section 9's actual requirements — signature verification, duplicate/
out-of-order webhook handling — untested and effectively unbuilt. Building the
real mechanism against a fake provider means swapping in a real one later is
adding one class, not retrofitting the webhook path.
Status: Accepted (Phase 9).

---
**ADR-018**
Decision: `MarkOrderAsPaidCommand` (`Ordering.Contracts`) is dispatched by
`Payments.Application`'s webhook handler — the reverse direction of ADR-014
(Ordering calling Catalog/Inventory at checkout; here Payments calls
Ordering). Same rule applies: only through the target module's `*.Contracts`
command via the shared `IDispatcher`, never a direct reference.
Reason: a successful payment must update the Order's PaymentStatus
immediately (the confirmation page reads it right after), which rules out an
async integration event — same reasoning as ADR-014, just the other way round
between two different module pairs. Confirms ADR-014's pattern generalizes
symmetrically rather than only working in one direction.
Status: Accepted (Phase 9).

---
**ADR-019**
Decision: `tests/IntegrationTests` disables xUnit's default test-class
parallelization (`xunit.runner.json`: `parallelizeAssembly`/
`parallelizeTestCollections: false`).
Reason: these tests share one real, mutable LocalDB database (docs/testing.md).
Running test classes in parallel produced a real, reproducible failure —
`PaymentWebhookTests` and other classes executing concurrently caused a
spurious `DbUpdateConcurrencyException` in an unrelated test's
`PlaceOrderCommandHandler` call, purely from parallel execution against
shared state, not a code bug (isolated re-runs of the same test always
passed). Sequential execution is the correct default for integration tests
against one shared external resource — parallelism there buys speed at the
cost of exactly this class of flakiness.
Status: Accepted (Phase 9).

---
**ADR-020**
Decision: `Store.Worker`'s Outbox processor is a generic `OutboxProcessingService<TContext>`
(`Persistence.Outbox`) — one instance per module context, registered via
`services.AddOutboxProcessor<TContext>()` — paired with an in-process `IEventBus`
(`EventBus.InProcessEventBus`, resolves `IIntegrationEventHandler<TEvent>` from DI and invokes
whatever's registered). Both live in existing BuildingBlocks (Persistence/EventBus), not a new one.
Reason: every module's `AppDbContextBase` already has an identical `OutboxMessages` table
(ADR-005/008) — a generic-over-`TContext` processor means adding a new module's outbox to the
worker later is one `AddXModule` + one `AddOutboxProcessor<XDbContext>()` line, not a bespoke
poller per module. "In-process" (not a real broker) matches the Phase 1 `IEventBus` doc comment's
promise for a modular monolith on one deployable — swapping to a real broker later is a new
`IEventBus` implementation, not an Application/Domain change anywhere.
Real bug found: `AppDbContextBase.EnqueueOutboxMessage` originally stored `eventType.FullName`;
the processor's `Type.GetType`/assembly-scan couldn't resolve `Payments.Contracts.PaymentSucceededIntegrationEvent`
because that assembly had never actually been JIT-loaded inside the worker process (a
`ProjectReference` alone doesn't load an assembly — .NET loads lazily on first real use, and
nothing in the worker's executed code path happened to touch that type). Fixed by storing
`AssemblyQualifiedName` instead — `Type.GetType(...)` given that form loads the declaring assembly
itself if needed, not just search already-loaded ones. Verified against the real dev DB: all
pending Ordering/Payments outbox rows (some pre-dating the fix) processed with zero errors after
the change.
Status: Accepted (Phase 10).

---
**ADR-021**
Decision: the Admin panel (Phase 11) is a `Store.Web` Area (`Areas/Admin`), not a separate
deployable or module — thin controllers dispatching the same `ICommand`/`IQuery` types the
storefront uses, gated per-action with `[Authorize(Policy = Permissions.X)]` (never role-name
checks — same rule as everywhere else). Every write action is a wrapper one step removed from an
existing aggregate behavior method (`Product.Publish()`, `Order.Cancel()`,
`StockItem.AdjustTo()`) — no new business rules were invented for the admin panel, only new
callers of rules that already existed. New read-side query interfaces
(`Ordering.Application.Checkout.IOrderQueries`, `Inventory.Application.Stock.IStockQueries`) mirror
`Catalog.Application.Products.IProductQueries`'s existing write/read split rather than introducing
a different pattern for admin listings.
The layout is a small hand-written stylesheet (`wwwroot/admin/admin.css`) on top of the storefront's
already-loaded Bootstrap bundle — deliberately *not* a curated subset of the `admin-ecomus`
ThemeForest template the way Phase 5 curated the storefront theme. Reason: admin-ecomus integration
is a comparable-sized effort to Phase 5's storefront curation for zero functional difference at
this stage; a minimal but real, working panel now is worth more than a half-integrated template.
Revisit if/when the visual polish is actually requested.
A dev-only `AdminUserBootstrapper` (`Identity.Infrastructure.Seeding`) creates one pre-confirmed
admin user from `Identity:DefaultAdmin:Email`/`Password` config *if set* — mirrors
`PermissionRoleSeeder`'s "safe to run every startup, does nothing if not configured" shape, but
unlike the webhook secret, these are real login credentials: never put them in `appsettings.json`,
User Secrets/environment variables only (see docs/security.md).
Status: Accepted (Phase 11).

---
**ADR-022**
Decision: Serilog (not the built-in `Microsoft.Extensions.Logging` console formatter) for
structured logging in both composition roots, code-configured (not read from
`appsettings.json`'s `Serilog` section) — Console + a rolling daily file sink, both composition
roots wrapped in the documented two-stage bootstrap-logger `try/catch/finally` pattern. Health
checks via `Microsoft.Extensions.Diagnostics.HealthChecks` + `AddDbContextCheck<T>()` per module
context: `Store.Web` exposes `GET /health`; `Store.Worker` (no inbound HTTP — plain
`Microsoft.NET.Sdk.Worker` host) uses `IHealthCheckPublisher` to log the same checks on a timer
instead.
Reason: `LogContextKeys`/`ICorrelationIdProvider` were built in Phase 1 specifically anticipating
this — Serilog's `ILogger.BeginScope` bridge (any MEL `BeginScope` dictionary becomes structured
Serilog properties automatically, no extra wiring) means the existing
`GlobalExceptionHandler`/module code that already calls `ILogger`/`BeginScope` didn't need to
change its logging calls at all, just gained structure. Code-configured over
config-file-driven: this project's rule throughout (ADR-003 module composition, ADR-010 explicit
handler registration) has been "explicit C# over reflection/config magic" — one more config
surface with its own schema/validation isn't worth it for two sinks. `AddDbContextCheck<T>` over
a custom health check: "is the DB reachable" is genuinely all that's needed at this scale: a
failing check already means "look at the logs", not something a fancier per-table check would
add information to.
Status: Accepted (Phase 12).

---
**ADR-023**
Decision: `docker-compose.yml`'s build context for both `Store.Web`/`Store.Worker` is the repo
root, not each project's own folder — `dotnet restore <single csproj>` inside the image (not the
`.slnx`), `.dockerignore` excludes `tests/`, `ecomus-package/`, `Mecodex-Brand-Assets/`, `docs/`.
Migrations apply automatically inside the containers via an opt-in `ApplyMigrationsOnStartup`
config flag (`Persistence.MigrationExtensions.MigrateWithRetryAsync<TContext>`, retry-with-delay)
— local `dotnet run` never sets it, so the existing manual `dotnet ef database update` workflow is
unaffected. A `redis` container is provisioned in the compose stack even though no application
code uses it yet.
Reason: Central Package Management (`Directory.Packages.props`) lives at the repo root and this
solution's projects reference each other across folder boundaries, so a project-folder-scoped
build context can't restore correctly — the root is the only context that has everything a
`ProjectReference` chain might need, and restoring one project (not the whole `.slnx`) keeps
`tests/` (and everything a module doesn't actually depend on) out of the image for free, no
separate exclusion list to maintain. Auto-migration exists specifically because Compose has no
interactive step to run `dotnet ef database update` against a freshly-started SQL Server
container the way local dev does — without it, `docker compose up` would start a Store.Web that
immediately 500s on every DB-touching request. Retry-with-delay because the `sqlserver`
container's own health check can report healthy a moment before it's actually accepting every
connection reliably. Redis: provisioned ahead of the code that will use it because the original
spec named it as part of the stack; standing up infrastructure ahead of need is not the kind of
speculative C# abstraction ADR-guidance elsewhere warns against — it's ordinary environment
provisioning, and it costs nothing sitting idle in a compose file.
Status: Accepted (Phase 13).

---
**ADR-024**
Decision: `.github/workflows/build-test.yml` runs on `windows-latest`, not `ubuntu-latest`, and
does not stand up a SQL Server service container the way `docker-compose.yml` does.
Reason: every `IntegrationTests` file hardcodes its own
`Server=(localdb)\mssqllocaldb;Database=ECommerce;...` connection string constant rather than
reading one from configuration (docs/testing.md), and every module's `IDesignTimeDbContextFactory`
does the same for `dotnet ef` commands — both match ADR-011..019's dev workflow via *actual*
LocalDB, not a container. Rather than treat that as CI debt to refactor (parameterize every test
file's connection string, add a SQL Server service container, wire migrations against it), the
lower-risk option was recognizing GitHub's `windows-latest` runner image ships SQL Server Express
LocalDB preinstalled under the exact same instance name (`MSSQLLocalDB`) — so the existing test
suite runs against CI completely unmodified, and CI now exercises the *same* connection path
every developer's machine already does, rather than a second, parallel one (container-based) that
could drift from it. `ubuntu-latest` was rejected specifically because LocalDB is Windows-only —
no amount of reachable-service-container workaround changes that.
Status: Accepted (Phase 14). Revisit only if this repo later needs Linux-only CI runners for cost
or speed reasons — at that point, ADR-011..019's "real LocalDB" testing assumption itself would
need to change first (e.g. to a SQL Server container everywhere, dev machines included), which is
a bigger decision than a CI workflow file.

---
**ADR-025**
Decision: `Order` gained an `Email` property, collected explicitly at checkout regardless of
guest/authenticated status. `OrderPlacedIntegrationEvent` carries it directly;
`PaymentSucceededIntegrationEvent` still doesn't (unchanged shape), so its consumer dispatches a
new `Ordering.Contracts.GetOrderContactInfoQuery` (ADR-014) to look the email up instead.
Reason: building Notifications (Phase 15) surfaced a real gap — nothing in Ordering ever
collected an email anywhere (`Address` doesn't have one; guests have no `ApplicationUser` to look
one up from), so order-confirmation/payment-receipt emails were structurally impossible before
this. Threading it onto `Order` directly (not a new `Customers`-owned concept, since Customers
isn't built) keeps it in the one place checkout already writes, at the cost of one new required
column + one new migration. Not adding it to `PaymentSucceededIntegrationEvent` too was deliberate
— duplicating the email onto every event that might need it invites the two copies drifting if an
order's contact info is ever corrected; a single dispatched read from Ordering is the same "ask
the owner" rule ADR-014 already established, applied consistently rather than special-cased away
because it's "just an email field."
Status: Accepted (Phase 15).

---
**ADR-026**
Decision: Notifications (Phase 15, first of the five previously-placeholder modules to get real
code) owns a plain `NotificationLog` entity (not an aggregate root — no business rules, nothing
ever transitions its state after creation) and an `INotificationSender` abstraction with
`FakeEmailSender` as the only implementation, same shape as Payments' `IPaymentGateway`/
`FakePaymentGateway` (ADR-017). Wired into `Store.Worker` (where `IEventBus` actually dispatches)
and, DbContext-only, into `Store.Web` (for `/health` and future admin log viewing — its handlers
never fire there since Store.Web doesn't process the Outbox).
Reason: this is the first module whose entire reason to exist is reacting to other modules'
integration events — Catalog/Inventory/Ordering/Payments all *publish* real business state
changes; Notifications *consumes* them and produces a side effect (an email) with no state of its
own that anything else needs to read. That asymmetry is why it had no `Notifications.Contracts`
public surface at first (see ADR-027, one phase later, for when it got one): nothing consumes a
"notification was sent" fact. `FakeEmailSender` mirrors `FakePaymentGateway`'s reasoning exactly —
no real provider account exists, but the mechanism (interface → real `NotificationLog` write) is
real, not stubbed out.
Status: Accepted (Phase 15).

---
**ADR-027**
Decision: Notifications gained a *dispatchable* `SendEmailCommand` (`Notifications.Contracts`,
ADR-014) alongside its event-*reactive* handlers (ADR-026) — any module's Application layer may
send an email synchronously via the shared `IDispatcher`, not just react to a fact that already
happened. First (and so far only) caller: `Store.Web.Controllers.AccountController` — a
registration-confirmation or password-reset link has to exist *before the response renders*
(there's no prior integration event to react to; "user registered" and "here is their
confirmation link" are the same moment, not two separate facts).
Reason: forcing every email through the event-reactive path would mean inventing a fake
integration event just to trigger it (e.g. a `UserRegisteredIntegrationEvent` Identity would have
to publish through an Outbox it doesn't have — `AppIdentityDbContext` derives ASP.NET Core
Identity's own `IdentityDbContext`, not `AppDbContextBase`, so it has no
`EnqueueOutboxMessage`/Outbox table at all, ADR-009). Adding Outbox support to a context that
exists specifically to stay framework-owned would be a bigger structural change than the actual
problem (send one email, now) calls for. A plain dispatched command is the same "ask the owner
module to do it" pattern ADR-014 already established for reads and cross-module writes, just
applied to "send an email" instead of "reserve stock" or "mark an order paid."
Status: Accepted (Phase 16).

---
**ADR-028**
Decision: Customers (Phase 17, second of the five placeholder modules to get real code) makes
`Customer.Id` deliberately equal to the owning `ApplicationUser.Id` from Identity — not a fresh
Guid with its own FK-style reference column. No `Customers.Contracts` public surface yet;
`Store.Web.Controllers.ProfileController` talks to it directly via `IDispatcher`, and checkout
(`PlaceOrderCommand`) still always passes `CustomerId: null` — this module is not wired into the
order-placement path yet.
Reason: a "customer profile" and "the account that logs in" are the same real-world entity here
(no B2B multi-user-per-account requirement exists), so a 1:1 relationship keyed by the same id is
simpler than inventing a separate `CustomerId` and a lookup between the two — and it still keeps
the module boundary real: neither module's DbContext/tables reference the other's, only the
Guid value happens to match, known only where it has to be (the one controller that reads
`ICurrentUser.UserId` and calls both). Not wiring it into checkout yet was a scope cut, not an
oversight — attaching a real `CustomerId` to a placed order and/or pre-filling checkout from a
saved default address both touch `PlaceOrderCommand`, which already has enough moving parts
(price re-validation, stock reservation with compensation, tax, and — as of Phase 18/19 — coupon
redemption and shipping cost lookup); bolting on a fifth cross-module concern in the same phase
that built the module owning it risked under-testing all of them. Revisit as its own phase.
Status: Accepted (Phase 17).

---
**ADR-029**
Decision: Promotions (Phase 18, third of the five placeholder modules to get real code) is the
first module wired into checkout using ADR-014's compensation pattern for something other than
stock. `RedeemCouponCommand`/`ReleaseCouponCommand` (`Promotions.Contracts`) mirror `Inventory.Contracts.ReserveStockCommand`/`ReleaseStockCommand`'s shape exactly: `PlaceOrderCommandHandler`
redeems the coupon (incrementing `Coupon.UsageCount`) right after computing the subtotal, and
releases it (decrementing back) if *anything* later in the same checkout fails — an invalid
address, a stock reservation failure, whatever comes next.
Reason: `Cart.ApplyCoupon` (Ordering, existing since Phase 7/8) only ever stored a code string —
no coupon actually existed to validate against until this phase, so "apply a coupon" and "the
discount actually applies" were two different, disconnected claims before now. Redeeming
immediately (not deferring validation to the very end) matches how price/stock are already
handled — fail fast, don't reserve/redeem things you might not need — and the compensation
pattern was already proven correct by Inventory's version rather than being invented fresh.
Coupon.Redeem caps a fixed-amount discount at the order's own subtotal (never produces a negative
total) and validates currency/expiry/usage-limit/minimum-order-amount together, atomically, so a
racing double-redemption of the last use of a limited coupon can't both succeed (ordinary EF
Core optimistic concurrency on the row — no special locking needed, same as everywhere else that
doesn't get ADR-006's explicit rowversion treatment because the contention window here is tiny).
Status: Accepted (Phase 18).

---
**ADR-030**
Decision: Shipping (Phase 19, fourth of the five placeholder modules to get real code) is a
single `ShippingMethod` aggregate — `Name`/`Description`/`Cost` (Money)/`EstimatedDaysMin`/`Max`/
`IsActive` — with no `ShippingZone` modeling: every active method applies everywhere, there is no
per-country/region rate matching. `PlaceOrderCommand`'s `ShippingCost: decimal` parameter is
replaced outright with `ShippingMethodId: Guid`; the handler dispatches
`Shipping.Contracts.GetShippingMethodQuery` (ADR-014) right after tax computation to get the
authoritative cost, exactly like Catalog's price and Inventory's stock are never trusted from the
client. Checkout's UI (`GET /Checkout`) now dispatches `ListShippingMethodsQuery()` to render a
real radio-button picker instead of a hidden flat `50m`.
Reason: the flat `50m` shipping cost hardcoded into checkout since Phase 7/8 was the last
remaining "fake number in the write path" identified in the Phase 15-18 gap analysis (see
current-state.md) — Catalog pricing and Inventory stock were already real by that point, so
shipping was the odd one out. A `ShippingMethod` aggregate with a picker is the minimum real
version of "shipping is a choice with a real cost," matching the same re-validate-server-side
discipline as every other checkout input. Zone/region rate modeling is a real, separate feature
(different methods costing different amounts in different countries) that would roughly double
the aggregate's complexity for no proven near-term need — deliberately deferred, not missed.
Status: Accepted (Phase 19).

---
**ADR-031**
Decision: Reviews (Phase 20, last of the five placeholder modules to get real code) is a single
`Review` aggregate — `ProductId`/`ReviewerName`/`ReviewerEmail`/`Rating` (1-5)/`Title`/`Body`/
`Status`. Every review starts `Pending`; `SubmitReviewCommand` (storefront, no login required —
same guest-friendly posture as checkout) never makes a review visible on its own. Only an admin
`ApproveReviewCommand`/`RejectReviewCommand` moves it out of `Pending`, and
`GetProductReviewsQuery` (the storefront's product-page query) only ever returns `Approved` ones.
No "verified purchase" check against Ordering — a review is accepted from anyone regardless of
whether they actually bought the product.
Reason: free-text, publicly-displayed user content is a different trust problem than a price or a
stock count — the risk isn't a wrong number, it's spam/abuse appearing on a live product page.
Moderation-before-publish is the minimum real version of that: `Review.Approve`/`Reject` are
one-way (`Review.NotPending` blocks re-moderating an already-decided review), same "guarded
transition, no silent re-entry" shape as every other status machine in this system (`Order`,
`Coupon`, `ShippingMethod`). "Verified purchase" (cross-checking Ordering for a real completed
order before accepting a review) is a real, separate feature — deliberately deferred, not
attempted here, same reasoning as Shipping's zone modeling (ADR-030) and Customers not being
wired into checkout yet (ADR-028): each of these five placeholder modules got exactly the real
code its phase needed, not everything imaginable for it.
Status: Accepted (Phase 20).

---
**ADR-032**
Decision: Phase 21 closes two long-standing admin gaps rather than adding a new module: (1)
Brand/Category admin management — `Catalog.Application.Brands`/`Categories` (list/create/
activate/deactivate, mirroring Promotions'/Shipping's admin command shape exactly) plus wiring
the existing Product Create/Edit admin form to actually pick a `BrandId` and `CategoryIds` from
real data instead of always sending `null` (`Product.SetBrand`/`SetCategories` added to the
aggregate for the Edit path); (2) a Payments admin UI — `ListPaymentsQuery`/`IPaymentsQueries`
(admin-wide or narrowed to one order) plus a `PaymentsController` that can trigger
`RefundPaymentCommand` (already existed since Phase 9, just never had an admin surface). No new
permission categories were needed for either — Catalog and Payments already had
`View`/`Create`/`Edit`/`Refund` etc. from Phase 11, they just weren't wired to a controller yet.
Reason: these were the two items explicitly named in the original gap analysis ("Admin panel: no
Brand/Category management UI... no Payments admin UI") — both were cases where the domain/
application code (or most of it) already existed from earlier phases and only the admin
composition (controller + view + the one missing `IPaymentsQueries` read-side) was missing, unlike
the five placeholder modules (ADR-025/026/028/029/030/031) which needed everything built from
scratch. Treating "wire up what already exists" as its own phase kept the diff reviewable instead
of bundling it into a module phase it doesn't belong to.
Status: Accepted (Phase 21).

---
**ADR-033**
Decision: Phase 22 gives the Redis container (provisioned since Phase 13, unused until now) its
first real reader — a read-through cache in front of Catalog's two storefront-facing queries
(`GetBySlugAsync`/`SearchAsync`), via a new `BuildingBlocks/Caching` project and a
`CachedProductQueries : IProductQueries` decorator in `Catalog.Infrastructure`. TTL-only
invalidation (60s for a product page, 30s for search/listing) — no write-side cache eviction on
`Create`/`Update`/`Publish`/`Archive`/`Delete`. `GetVariantSnapshotAsync` (checkout's price/stock
re-validation, ADR-014) and admin listings (`IncludeAllStatuses: true`) are never cached — a stale
price or a Draft product missing from the admin list right after creating it are correctness bugs,
not acceptable staleness. `Caching.AddDistributedCaching` registers real Redis
(`AddStackExchangeRedisCache`) when `ConnectionStrings:Redis` is configured (docker-compose.yml
sets it) and falls back to `AddDistributedMemoryCache` otherwise — same "the app never depends on
this running" posture as `ApplyMigrationsOnStartup`/`AdminUserBootstrapper`. `AddCatalogModule`
itself also calls `AddDistributedMemoryCache()` as a `TryAdd`-based safety net, so any composition
that never calls `AddDistributedCaching` (every integration test, potentially `Store.Worker` if it
ever added this module) still resolves `IDistributedCache` instead of crashing — the composition
root's own real registration wins wherever it ran first, since `TryAdd` no-ops once any
implementation is present.
Reason: TTL-only invalidation was chosen over write-side eviction (a version-stamped key, or an
explicit evict-on-write call from every product command handler) because the latter adds a
cross-cutting concern to every `Catalog.Application` write handler for a benefit — sub-60-second
freshness instead of up-to-60-second freshness — that a demo-scale storefront doesn't need; the
one place staleness would be a real bug (checkout pricing) is the one place that was deliberately
excluded from caching altogether. Verified against a real local Redis instance (not just the
in-memory fallback): confirmed real keys/TTLs in Redis after visiting a product page, confirmed a
direct DB mutation was *not* reflected until the cached entry's TTL expired (proving the cache was
actually being served, not silently bypassed), and confirmed the mutated value appeared
automatically once the TTL lapsed (self-healing, no manual invalidation needed).
Status: Accepted (Phase 22).

---
**ADR-034**
Decision: Phase 23 adds a `publish-images` job to `.github/workflows/build-test.yml` — builds and
pushes both `Store.Web`/`Store.Worker` images to GHCR (`ghcr.io/<owner>/<repo>-store-web`/
`...-store-worker`, tagged with the commit SHA and `latest`) on every push to `main`/`master`,
gated on `build-and-test` passing first, using the workflow's own `GITHUB_TOKEN` (no extra secret
to provision). Runs on `ubuntu-latest`, separate from `build-and-test`'s `windows-latest` (image
builds need a Linux Docker daemon; LocalDB needs Windows — ADR-024). The same commit also fixed a
real, previously-unnoticed bug: `build-and-test`'s per-context `dotnet ef database update` list
still only covered the five contexts that existed when Phase 14 wrote it (Catalog/Inventory/
Ordering/Payments/Identity) — every context Phases 15/17/18/19/20 added
(Notifications/Customers/Promotions/Shipping/Reviews) was silently missing, meaning
IntegrationTests would have started failing on this workflow the moment any test touched one of
those five modules' tables, with no one having actually run it since a remote doesn't exist yet
to trigger it on.
Reason: image publish was the other Phase 14 gap named alongside branch protection in the original
analysis (the latter needs a GitHub remote this repo doesn't have yet, genuinely out of reach, not
deferred by choice). A real `docker compose up --build`/`docker build` run to verify the images
actually work was attempted this session (not assumed unavailable from Phase 13's note) — Docker
Desktop's backend process was launched and observed exiting within ~15 seconds every time (nested
virtualization unavailable in this sandbox). Absent a running daemon, verification fell back to
the closest available substitutes: `docker compose config` (validates and interpolates
`docker-compose.yml`, including Phase 22's new `ConnectionStrings__Redis`, without needing a
daemon), the workflow YAML parsing correctly end to end, and — the strongest substitute — running
the exact `dotnet restore`/`dotnet publish` commands each Dockerfile's `RUN` steps execute against
a byte-for-byte copy of the Dockerfiles' own build context, which succeeded for both projects and
produced the exact DLLs each `ENTRYPOINT` expects. The one thing genuinely unverified is the
container-runtime layer itself — worth a real `docker compose up --build` pass in an environment
where Docker Desktop can actually start.
Status: Accepted (Phase 23).

---
**ADR-035**
Decision: Phase 24 replaces the Admin area's hand-styled placeholder chrome (`admin-shell`/
`admin-sidebar`/`admin-table`, `wwwroot/admin/admin.css`) with a real integration of the
`admin-ecomus` ThemeForest template — the same curated-subset approach ADR (Phase 5) used for the
storefront, applied to the admin dashboard template this time. A curated ~1.3MB asset subset
(`css/`, a hand-picked `js/` set — `jquery`/`bootstrap`/`bootstrap-select`/`main`/`theme-settings`,
explicitly not `apexcharts`/`morris`/`raphael`/`jvectormap`, none of which anything here wires up
— `font/`, `icon/`) is copied into `wwwroot/admin-ecomus/`, out of the raw ~5MB/137-file
`ecomus-package/ecomus/admin-ecomus/` source (never served directly, same as the storefront's
package). `_AdminLayout.cshtml` is rebuilt on the template's real sidebar/header-dashboard/
main-content structure — active-menu-item highlighting computed from `RouteData`, a real dark/
light toggle (`theme-settings.js`, `localStorage`-backed), a real signed-in user's name in the
header, real `Sign out` — and every existing admin view (Dashboard, Products, Brands, Categories,
Orders, Payments, Stock, Coupons, ShippingMethods, Reviews — 18 view files across 10 controllers)
is retargeted onto the template's own component classes (`wg-box`, `wg-table`/`item-row`,
`form-style-1`/`fieldset`, `tf-button`, `block-available`/`block-stock` status pills) instead of
being ported to new bespoke markup per page. `wwwroot/admin/admin-overrides.css` keeps only what
the theme has no component for: alert banners (success/error) and a small dashboard stat-card icon
badge.
Reason: rebuilding all 18 pages' internal DOM to literally match a specific demo page's exact
per-page structure (the raw template's `product-list.html` and `category-list.html` use
differently-named scoped grid classes, `table-product-list` vs `table-all-category`, that would
each need their own bespoke CSS if copied verbatim) would multiply the surface area for visual
bugs for no functional gain over reusing one consistent `wg-table` row-list shape with real
per-page columns — the theme's own CSS already makes that shape look native everywhere it's used
in the source template, not just on one demo page. No chart libraries were wired up (dashboard
KPI cards show real counts, no apexcharts-rendered trend lines) because there's no historical
time-series data anywhere in this system to chart honestly — a fabricated "1.56%" trend arrow
next to a real number would be dishonest UI, not a design choice. Fake demo content already in
the template (notification/inbox dropdowns with invented unread counts, a country-language
switcher, stock product photography) was dropped entirely for the same reason: only the pieces
backed by something real (real nav, real user, real dark-mode preference, real data tables) made
it in. Verified live in-browser: signed in as the seeded admin, confirmed the sidebar/header CSS
computes real values (`320px` fixed sidebar, `12px`-radius `wg-box` cards, `icomoon` icon font
resolving), confirmed the icon font and all curated JS/CSS assets loaded 200 (not 404), exercised
a real Deactivate/Activate round-trip on a seeded Brand and watched the themed success alert and
status pill flip live, viewed a real seeded Order's detail page, and confirmed the sidebar
collapses off-canvas at a mobile viewport width (the theme's own responsive behavior, not
something hand-rolled here).
Status: Accepted (Phase 24).
