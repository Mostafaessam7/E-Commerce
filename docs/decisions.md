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
