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
  repository interfaces if needed). Depends on own Domain, sanctioned BuildingBlocks
  (`Security`, `Infrastructure`, `Messaging`, `EventBus`), and **any module's** `*.Contracts`
  (ADR-014 — the sanctioned way to call another module synchronously; never that module's
  Domain/Application/Infrastructure directly).
- **Infrastructure**: EF Core `DbContext`, entity configurations, repository
  implementations, external service clients. Depends on own Application +
  `Observability`.
- **Contracts**: the module's public surface for other modules — DTOs, integration event records,
  and (ADR-014) dispatchable commands/queries other modules' Application layers may reference.
  Depends only on `SharedKernel` + `EventBus` + `Messaging` (the last one specifically so it can
  host `ICommand<T>`/`IQuery<T>` types, not just plain DTOs).

## Dependency rules (enforced by `tests/ArchitectureTests`)

```
SharedKernel (zero deps)
  ↑
Domain / EventBus / Messaging
  ↑
Contracts → SharedKernel, EventBus, Messaging
  ↑
Application → own Domain, Security, Infrastructure(BB), EventBus, Messaging, ANY module's Contracts
  ↑
Infrastructure → Observability
  ↑
Store.Web / Store.Worker (composition root)
```

No module references another module's Domain/Application/Infrastructure —
only that module's `*.Contracts`, and only from Application (ADR-014). Nothing below the
composition root references `Store.Web` or `Store.Worker`.

## Module communication

Two mechanisms, for different needs — don't reach for the wrong one (docs/events.md elaborates on
telling them apart):

- **Synchronous cross-module call** (ADR-014, first used Phase 7/8): one module's Application
  layer dispatches a command/query defined in *another* module's `*.Contracts` project, through
  the shared `Messaging.IDispatcher` — never a direct reference to that module's Domain/
  Application/Infrastructure. For "is this true *right now*, before I commit" needs: Ordering
  calls Catalog (`GetProductVariantSnapshotQuery`, re-validate price/availability), Inventory
  (`ReserveStockCommand`/`ReleaseStockCommand`), Promotions (`RedeemCouponCommand`/
  `ReleaseCouponCommand`, ADR-029, same reserve/release compensation shape as Inventory's), and
  Shipping (`GetShippingMethodQuery`, ADR-030) at checkout; Payments calls Ordering
  (`MarkOrderAsPaidCommand`, ADR-018) once a webhook confirms a payment; Notifications calls
  Ordering (`GetOrderContactInfoQuery`, ADR-025) when a payment-succeeded event carries no email.
  `*.Contracts` may reference `Messaging` specifically so it can host these dispatchable request
  types, not only DTOs/integration events (ArchitectureTests enforce this — any module's
  Application may reference any module's Contracts, never another module's Domain/Application/
  Infrastructure).
- **Cross-module side effect, eventual** (Phase 2 write-side, Phase 10 processor): publish an
  `IIntegrationEvent` via `IEventBus`, written to the Outbox in the same DB transaction as the
  triggering change. `Store.Worker` polls it and dispatches through the in-process `IEventBus`;
  the consuming module's Application layer implements `IIntegrationEventHandler<TEvent>`,
  idempotently (see `docs/events.md`). Two publishers (`OrderPlacedIntegrationEvent`,
  `PaymentSucceededIntegrationEvent`), one consumer since Phase 15 (Notifications — both handlers
  write a `NotificationLog` row).
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
