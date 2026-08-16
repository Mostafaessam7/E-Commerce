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
own that anything else needs to read. That asymmetry is why it has no `Notifications.Contracts`
public surface yet: nothing consumes a "notification was sent" fact. `FakeEmailSender` mirrors
`FakePaymentGateway`'s reasoning exactly — no real provider account exists, but the mechanism
(interface → real `NotificationLog` write) is real, not stubbed out.
Status: Accepted (Phase 15).
