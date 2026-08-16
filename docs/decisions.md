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
