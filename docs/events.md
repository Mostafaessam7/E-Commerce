# Events

## Domain Events (in-process, same aggregate/transaction)

`SharedKernel.Primitives.IDomainEvent` / `DomainEvent`. Raised inside an
aggregate root via `RaiseDomainEvent(...)`, collected on `Entity.DomainEvents`,
dispatched by the Application layer after `SaveChangesAsync` succeeds. Not
for cross-module communication.

## Integration Events (cross-module)

`EventBus.IIntegrationEvent` / `IntegrationEvent` / `IEventBus` /
`IIntegrationEventHandler<T>` (Phase 1 abstractions; `EventBus.InProcessEventBus` is the real
transport since Phase 10 — see "Outbox" below). Concrete events live in the publishing module's
`*.Contracts` project so consumers depend only on the DTO shape, never the publisher's
Domain/Application.

## Outbox (write-side: Phase 2, processor: Phase 10)

Publishing an integration event writes an OutboxMessage row in the same DB
transaction as the change that caused it — never publish directly after
`SaveChangesAsync`. `Store.Worker` runs one `Persistence.Outbox.OutboxProcessingService<TContext>`
per module context that enqueues events (currently `OrderingDbContext`, `PaymentsDbContext` —
`AddOutboxProcessor<TContext>()`), polling unprocessed rows and dispatching through
`EventBus.InProcessEventBus` (resolves `IIntegrationEventHandler<TEvent>` from DI, in-process —
ADR-020), marking `ProcessedOnUtc`. At-least-once delivery: `IIntegrationEventHandler`
implementations must be idempotent (dedupe on `EventId`). First real consumer since Phase 15:
Notifications' `OrderPlacedNotificationHandler`/`PaymentSucceededNotificationHandler` — both
idempotent by construction (a duplicate confirmation email from an at-least-once redelivery is
harmless, unlike double-charging a payment, so no dedupe ledger is needed the way Payments'
webhook handler needs one).
`OutboxMessage.Type` stores the event's `AssemblyQualifiedName`, not just `FullName` — required
for `Type.GetType(...)` to load the declaring assembly if the worker process hasn't already
touched it (see ADR-020's real bug).

## Naming convention

`{Entity}{PastTenseAction}IntegrationEvent`, e.g. `OrderPlacedIntegrationEvent`,
`PaymentSucceededIntegrationEvent`, `InventoryReservedIntegrationEvent`.

## `OrderPlacedIntegrationEvent`

`Ordering.Contracts.OrderPlacedIntegrationEvent`, enqueued via
`IOrderingUnitOfWork.EnqueueIntegrationEvent(...)` → `OrderingDbContext.EnqueueIntegrationEvent(...)`
(a public wrapper around the protected `AppDbContextBase.EnqueueOutboxMessage`) inside
`PlaceOrderCommandHandler`, in the same `SaveChangesAsync` call as the Order insert. Consumed by
Notifications' `OrderPlacedNotificationHandler` since Phase 15 (writes a `NotificationLog` row,
Order confirmation).

## `PaymentSucceededIntegrationEvent`

`Payments.Contracts.PaymentSucceededIntegrationEvent`, enqueued via `IPaymentsUnitOfWork.EnqueueIntegrationEvent(...)`
inside `ProcessWebhookCommand`'s handler, same transaction as the `PaymentTransaction` state
change. Consumed by Notifications' `PaymentSucceededNotificationHandler` since Phase 15 (payment
receipt) — that handler carries no email of its own, so it dispatches
`Ordering.Contracts.GetOrderContactInfoQuery` (ADR-014) to look one up. Note this event does
**not** update Order state — see below.

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
