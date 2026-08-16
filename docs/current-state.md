Current Phase:
Phase 1 complete. Starting Phase 2 (Database) + Phase 3 (Identity).

Completed:
- Solution scaffolded: 51 projects, dependency graph wired per docs/architecture.md.
- SharedKernel: Entity/AggregateRoot, ValueObject, Money, Result/Error,
  IAuditableEntity/ISoftDeletableEntity + AuditableEntity base, Exceptions.
- BuildingBlocks: EventBus (abstractions only), Observability
  (ICorrelationIdProvider), Security (ICurrentUser, Permissions), Infrastructure
  (IDateTimeProvider).
- Store.Web: global exception handling (GlobalExceptionHandler,
  ResultExtensions), module composition convention.
- ArchitectureTests (29 passing) + UnitTests (28 passing).
- Committed: 71e7f96.

In Progress:
- (nothing — between phases)

Next:
- Phase 2: per-module DbContext, Fluent configs, auditing/outbox interceptors,
  migrations strategy.
- Phase 3: Identity module (ASP.NET Core Identity, roles, permission policies).

Known Issues:
None.

Important Files:
- AGENTS.md — entry point for any new session.
- docs/architecture.md, docs/modules.md — boundaries, don't re-derive from code.
- docs/decisions.md — ADR log, check before proposing an architecture change.

Database Changes:
None yet.

Decisions Made:
See docs/decisions.md (ADR-001..007).
