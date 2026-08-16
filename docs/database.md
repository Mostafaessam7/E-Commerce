# Database

Status: not implemented yet (Phase 2). This file is updated as Phase 2 lands.

## Planned (Phase 2)

- One `DbContext` per module (`{Module}DbContext` in `{Module}.Infrastructure`),
  not one shared context — keeps modules independently migratable and
  enforces "no cross-module table access" at the type-system level.
- SQL Server, EF Core 10, Fluent configuration (`IEntityTypeConfiguration<T>`
  per aggregate, no data annotations on entities).
- `SaveChanges` interceptor for auditing (`CreatedAtUtc`/`CreatedBy`/...) via
  `IAuditableEntity`, and for soft-delete query filtering via
  `ISoftDeletableEntity` (both already defined in `SharedKernel.Auditing`,
  Phase 1).
- Outbox table + interceptor: domain changes and their outbox rows commit in
  the same transaction. Processor lives in `Store.Worker`, added Phase 10 —
  Phase 2 only adds the table + write-side interceptor.
- Optimistic concurrency: EF Core shadow rowversion property per aggregate
  root that needs it (Inventory first), not a domain-model property — see
  ADR in `decisions.md`.
- Migrations: one migrations project/folder per module's Infrastructure
  project, applied independently.
