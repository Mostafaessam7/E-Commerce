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

## First real implementation: `OrderPlacedIntegrationEvent`

`Ordering.Contracts.OrderPlacedIntegrationEvent`, enqueued via
`IOrderingUnitOfWork.EnqueueIntegrationEvent(...)` → `OrderingDbContext.EnqueueIntegrationEvent(...)`
(a public wrapper around the protected `AppDbContextBase.EnqueueOutboxMessage`) inside
`PlaceOrderCommandHandler`, in the same `SaveChangesAsync` call as the Order insert. No consumer
yet (Payments/Notifications aren't built) and no processor yet (Phase 10) — the row just sits in
`ordering.OutboxMessages` until then.

## Second real implementation: `PaymentSucceededIntegrationEvent`

`Payments.Contracts.PaymentSucceededIntegrationEvent`, enqueued via `IPaymentsUnitOfWork.EnqueueIntegrationEvent(...)`
inside `ProcessWebhookCommand`'s handler, same transaction as the `PaymentTransaction` state
change. No consumer yet (Notifications isn't built) — sits in `payments.OutboxMessages` until
Phase 10's processor exists. Note this event does **not** update Order state — see below.

## Cross-module *synchronous* calls are not this

Domain/Integration events are for "tell other modules something happened, eventually". Checkout
needs "is this true *right now*" (current price, current stock) — that's ADR-014's dispatched
Contracts commands/queries via `IDispatcher`, a different mechanism entirely. Don't reach for an
integration event when what's actually needed is a synchronous cross-module read/write.

`ProcessWebhookCommand` is the second real example, and runs the *opposite* direction from
checkout's: Payments dispatches `Ordering.Contracts.MarkOrderAsPaidCommand` synchronously (the
confirmation page needs `PaymentStatus` to be current immediately, not eventually) — see ADR-018.
The `PaymentSucceededIntegrationEvent` enqueued in the same handler is a separate, independent
signal for whichever future consumer wants "a payment succeeded" (e.g. Notifications), not the
mechanism that updates the Order.
