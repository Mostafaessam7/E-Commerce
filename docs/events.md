# Events

## Domain Events (in-process, same aggregate/transaction)

`SharedKernel.Primitives.IDomainEvent` / `DomainEvent`. Raised inside an
aggregate root via `RaiseDomainEvent(...)`, collected on `Entity.DomainEvents`,
dispatched by the Application layer after `SaveChangesAsync` succeeds. Not
for cross-module communication.

## Integration Events (cross-module)

`EventBus.IIntegrationEvent` / `IntegrationEvent` / `IEventBus` /
`IIntegrationEventHandler<T>` (Phase 1 abstractions only — no transport yet).
Concrete events live in the publishing module's `*.Contracts` project so
consumers depend only on the DTO shape, never the publisher's Domain/Application.

## Outbox (Phase 2 write-side, Phase 10 processor)

Publishing an integration event writes an OutboxMessage row in the same DB
transaction as the change that caused it — never publish directly after
`SaveChangesAsync`. `Store.Worker` polls unprocessed rows and dispatches
through `IEventBus`, marking `ProcessedAtUtc`. At-least-once delivery:
`IIntegrationEventHandler` implementations must be idempotent (dedupe on
`EventId`).

## Naming convention

`{Entity}{PastTenseAction}IntegrationEvent`, e.g. `OrderPlacedIntegrationEvent`,
`PaymentSucceededIntegrationEvent`, `InventoryReservedIntegrationEvent`.
