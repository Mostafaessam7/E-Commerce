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
src/BuildingBlocks/  SharedKernel, EventBus, Observability, Security, Infrastructure
src/Modules/{Catalog,Inventory,Ordering,Payments,Customers,Identity,Promotions,Shipping,Reviews,Notifications}/
    each: *.Domain / *.Application / *.Infrastructure / *.Contracts
src/Web/Store.Web       composition root, MVC
src/Workers/Store.Worker  background host (Outbox processor from Phase 10)
tests/{UnitTests,IntegrationTests,ArchitectureTests,EndToEndTests}
```

Module details (responsibility/owns/contracts/deps): `docs/modules.md`.

## Module boundary rules (enforced by ArchitectureTests, do not violate)

`X.Domain` → SharedKernel only. `X.Contracts` → SharedKernel + EventBus.
`X.Application` → own Domain/Contracts + Security/Infrastructure(BB)/EventBus.
`X.Infrastructure` → own Application + Observability. No module references
another module's Domain/Application/Infrastructure — only its Contracts.
Nothing depends on Store.Web/Store.Worker. Full detail: `docs/architecture.md`.

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
```
(`dotnet test` does not accept multiple `.csproj` args — run one at a time.)

## Docs map

| File | Contents |
|---|---|
| `docs/architecture.md` | Layers, dependency rules, module communication |
| `docs/modules.md` | Per-module responsibility/owns/contracts/deps |
| `docs/coding-guidelines.md` | Naming, patterns, do/don't |
| `docs/database.md` | EF Core setup, DbContexts, migrations, Outbox |
| `docs/security.md` | Identity, permissions, policies |
| `docs/events.md` | Domain events vs integration events, Outbox flow |
| `docs/testing.md` | Test project conventions |
| `docs/decisions.md` | ADR log — don't re-litigate a recorded decision |
| `docs/current-state.md` | What's done, in progress, next — keep small |
| `docs/current-task.md` | Scratch pad for the task in flight, deleted when done |
