# AGENTS.md

Read this first in every new session. Then read `docs/current-state.md` and
`docs/current-task.md` (if present). Only read other `docs/*.md` files relevant to
the task at hand — see the map below.

## Project

Enterprise E-Commerce platform. ASP.NET Core MVC (Razor) storefront + admin,
.NET 10, EF Core, SQL Server, Redis. ThemeForest "Ecomus" HTML template
(`ecomus-package/`) converted to Razor views without breaking the design.

## Architecture style

Modular Monolith, DDD-lite, Clean Architecture layering, CQRS only where it
earns its keep, Domain Events (in-process) + Integration Events (cross-module,
via transactional Outbox). No microservices. Full rationale: `docs/architecture.md`.

## Solution structure

```
src/BuildingBlocks/  SharedKernel, EventBus, Observability, Security, Infrastructure, Persistence, Messaging, Caching
src/Modules/{Catalog,Inventory,Ordering,Payments,Customers,Identity,Promotions,Shipping,Reviews,Notifications}/
    each: *.Domain / *.Application / *.Infrastructure / *.Contracts
src/Web/Store.Web       composition root, MVC
src/Workers/Store.Worker  background host (Outbox processor from Phase 10)
tests/{UnitTests,IntegrationTests,ArchitectureTests,EndToEndTests}
```
(`Persistence` = EF Core-dependent shared code, `*.Infrastructure`-only, never
`*.Application` — ADR-008. `Messaging` = in-house CQRS dispatcher, `*.Application`-only — ADR-010.
`Caching` = `IDistributedCache`/Redis registration, `Store.Web`-only today — ADR-033, Phase 22.
All ten modules listed here have real code as of Phase 20 — none are placeholders anymore.)

## EF Core gotchas already paid for (read before touching a DbContext)

- Every module's DbContext needs its own SQL schema (`AppDbContextBase.SchemaName`) — ADR-011.
- Never give a value object `==`/`!=` — breaks EF's translation of comparisons against
  value-converted properties — ADR-013.
- Guid keys are handled automatically (`ValueGenerated.Never`, applied for every module by
  `AppDbContextBase`) — don't re-litigate, just know why (ADR-012) if a save mysteriously throws
  a concurrency exception with nothing else writing to the row.

## Other gotchas

- Never name an Application-layer command/query namespace after a Domain entity in the same
  module (e.g. not `Ordering.Application.Cart` next to `Ordering.Domain.Cart`) — C# resolves
  unqualified identifiers against sibling namespaces before `using` imports; use the plural or
  another distinguishing form — ADR-015.
- Need a synchronous cross-module read/write (not "eventually", *right now*)? That's ADR-014:
  define the command/query in the target module's `*.Contracts`, dispatch it via the shared
  `IDispatcher` — never reference another module's Application/Domain/Infrastructure directly.
  Works in both directions (Ordering→Catalog/Inventory, Payments→Ordering — ADR-018).
- Running the full `IntegrationTests` suite and seeing an unrelated spurious
  `DbUpdateConcurrencyException`? Check `tests/IntegrationTests/xunit.runner.json` exists and is
  copied to output — those tests share one real DB and must run sequentially, not in parallel
  (ADR-019).
- Adding a new integration event and it never seems to get dispatched by `Store.Worker`? Check
  `AppDbContextBase.EnqueueOutboxMessage` is storing `AssemblyQualifiedName` (it is, don't change
  it back to `FullName`) — see ADR-020.
- Admin panel work: `[Authorize(Policy = Permissions.X)]` per action, never
  `[Authorize(Roles = "Admin")]` — ADR-021. New admin commands should be a thin wrapper one step
  removed from an existing aggregate method, not a new business rule.
- Logging: use `ILogger`/`BeginScope` as normal — Serilog is wired as the provider (ADR-022) and
  bridges MEL scopes into structured properties automatically, nothing extra to do. Don't add a
  `Serilog` section to `appsettings.json`; sinks are code-configured in `Program.cs` on purpose.
- Docker: build context for both Dockerfiles is the repo root (Central Package Management needs
  it), never a project subfolder — `docker build -f src/Web/Store.Web/Dockerfile .` from root, not
  from inside `src/Web/Store.Web`. `ApplyMigrationsOnStartup` is Compose-only; never set it for
  local `dotnet run` (ADR-023).

Module details (responsibility/owns/contracts/deps): `docs/modules.md`.

## Module boundary rules (enforced by ArchitectureTests, do not violate)

`X.Domain` → SharedKernel only. `X.Contracts` → SharedKernel + EventBus + Messaging (the last one
so it can host ADR-014's dispatchable commands/queries). `X.Application` → own Domain + Security/
Infrastructure(BB)/EventBus/Messaging + **any module's** `*.Contracts` (ADR-014 — the sanctioned
way to call another module synchronously, never that module's Domain/Application/Infrastructure
directly). `X.Infrastructure` → own Application + Observability. Nothing depends on
Store.Web/Store.Worker. Full detail: `docs/architecture.md`.

## Top coding rules

- `Result<T>` for expected failures (business rules, validation, not found);
  custom exceptions (`SharedKernel.Exceptions`) only for unreachable-invariant
  cases. Never `throw` for a normal business outcome.
- Money is always `SharedKernel.ValueObjects.Money`, never raw `decimal`/`double`.
- Aggregate roots expose behavior methods (`order.MarkAsPaid()`), never public
  property setters for state transitions.
- Thin controllers. No business logic in controllers or Razor views.
- No generic repository. No abstraction without a concrete current need.
- Search for an existing type/pattern before adding a new one (see `decisions.md`).
- Full guidelines: `docs/coding-guidelines.md`.

## Build / test

```bash
dotnet build ECommerce.slnx
dotnet test tests/UnitTests/UnitTests.csproj
dotnet test tests/ArchitectureTests/ArchitectureTests.csproj
dotnet test tests/IntegrationTests/IntegrationTests.csproj
```
(`dotnet test` does not accept multiple `.csproj` args — run one at a time. IntegrationTests need
a real LocalDB with migrations applied — docs/database.md — and are slower, but skipping them for
anything touching persistence/cross-module dispatch has bitten real bugs before, e.g. ADR-020.)

## Docs map

| File | Contents |
|---|---|
| `docs/architecture.md` | Layers, dependency rules, module communication |
| `docs/modules.md` | Per-module responsibility/owns/contracts/deps |
| `docs/coding-guidelines.md` | Naming, patterns, do/don't |
| `docs/database.md` | EF Core setup, DbContexts, migrations, Outbox |
| `docs/security.md` | Identity, permissions, policies |
| `docs/events.md` | Domain events vs integration events, Outbox flow |
| `docs/observability.md` | Serilog, correlation id, health checks |
| `docs/deployment.md` | Docker/docker-compose, migrations-in-container, Redis provisioning |
| `docs/ci-cd.md` | GitHub Actions build+test workflow |
| `docs/testing.md` | Test project conventions |
| `docs/decisions.md` | ADR log — don't re-litigate a recorded decision |
| `docs/current-state.md` | What's done, in progress, next — keep small |
| `docs/current-task.md` | Scratch pad for the task in flight, deleted when done |
