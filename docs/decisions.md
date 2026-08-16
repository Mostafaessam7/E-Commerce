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
