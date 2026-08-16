# Architecture

## Style

Modular Monolith. One deployable (`Store.Web`) + one background host
(`Store.Worker`), ten independent modules, Clean Architecture layering inside
each module. CQRS applied only to handlers that benefit from it (not blanket
CRUD). Domain Events for in-aggregate side effects; Integration Events +
transactional Outbox for cross-module communication.

## Layers (per module)

- **Domain**: entities, aggregate roots, value objects, domain events, business
  rules. No framework dependencies. Depends only on `SharedKernel`.
- **Application**: use cases (commands/queries/handlers), orchestrates Domain,
  defines the interfaces Infrastructure implements (`I{Module}UnitOfWork`,
  repository interfaces if needed). Depends on own Domain/Contracts +
  `Security`, `Infrastructure`(BB), `EventBus`.
- **Infrastructure**: EF Core `DbContext`, entity configurations, repository
  implementations, external service clients. Depends on own Application +
  `Observability`.
- **Contracts**: the module's public surface for other modules — DTOs and
  integration event records other modules' Application layers may reference.
  Depends only on `SharedKernel` + `EventBus`.

## Dependency rules (enforced by `tests/ArchitectureTests`)

```
SharedKernel (zero deps)
  ↑
Domain / Contracts / EventBus
  ↑
Application → Security, Infrastructure(BB), EventBus
  ↑
Infrastructure → Observability
  ↑
Store.Web / Store.Worker (composition root)
```

No module references another module's Domain/Application/Infrastructure —
only that module's `*.Contracts`. Nothing below the composition root
references `Store.Web` or `Store.Worker`.

## Module communication

- **In-process, cross-module read**: Application layer references another
  module's `*.Contracts` DTOs (no such usage exists yet as of Phase 2/3 — add
  only when a real need appears).
- **Cross-module side effect**: publish an `IIntegrationEvent` via `IEventBus`,
  written to the Outbox in the same DB transaction as the triggering change.
  Consuming module's Application layer implements
  `IIntegrationEventHandler<TEvent>`, idempotently (see `docs/events.md`).
- **Never**: direct reference to another module's `DbContext`, entities, or
  repositories.

## Composition root

Each module's Infrastructure project exposes
`Add{Module}Module(IServiceCollection, IConfiguration)`, called explicitly
from `Store.Web/Program.cs` (and `Store.Worker/Program.cs` where relevant).
No reflection-based module discovery — see ADR-003 in `decisions.md`.

## Error handling

`Result<T>` (SharedKernel.Results) for expected failures. Custom exceptions
(SharedKernel.Exceptions: DomainException/ValidationException/NotFoundException/
ConflictException/UnauthorizedException/ForbiddenException) for unreachable
states only. Both map to identical ProblemDetails via
`Store.Web/Infrastructure/ExceptionHandling/HttpStatusCodeMapper.cs`. See
ADR-002.
