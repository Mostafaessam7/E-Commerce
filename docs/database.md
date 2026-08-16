# Database

- One `DbContext` per module (ADR-005), SQL Server, EF Core 10.
- **`Persistence` building block** (`src/BuildingBlocks/Persistence`) — the one place EF Core is
  referenced outside a module's own Infrastructure project. Referenced only by `*.Infrastructure`,
  never `*.Application` (ADR-008: a 6th building block, added specifically to keep EF Core out of
  Application's dependency graph).
  - `AppDbContextBase` — abstract DbContext base most modules derive from. Gives
    `DbSet<OutboxMessage> OutboxMessages`, auto-applies `IEntityTypeConfiguration<T>`s from the
    derived context's own assembly, applies the soft-delete query filter, and exposes a protected
    `EnqueueOutboxMessage(IIntegrationEvent)` helper.
  - `AuditingInterceptor` (`SaveChangesInterceptor`) — stamps `IAuditableEntity` fields. Registered
    via `DbContextOptions.AddInterceptors(...)`, not inheritance, so it also works for contexts
    that can't derive from `AppDbContextBase` (Identity — see below).
  - `ModelBuilderExtensions.ApplySoftDeleteQueryFilter()` — reflection-based, applies
    `HasQueryFilter(e => !e.IsDeleted)` to every `ISoftDeletableEntity` in the model.
  - `OutboxMessage`/`OutboxMessageConfiguration` — outbox table shape, identical across modules.
    Write-side only; the Phase 10 `Store.Worker` processor reads it.
  - Proven by `tests/IntegrationTests/Persistence/*` (EF Core InMemory): auditing, soft-delete
    filter, outbox enqueue all covered.
- **Identity is the exception**: `AppIdentityDbContext` derives from ASP.NET Core Identity's
  `IdentityDbContext<...>` (C# has no multiple inheritance), so it doesn't get
  `AppDbContextBase`'s outbox/soft-delete conventions. Auditing still applies (interceptor wired
  through `DbContextOptions` in `Identity.Infrastructure/DependencyInjection.cs`).
- **Migrations**: one migrations folder per module's Infrastructure project, applied
  independently. Not created yet — no module has entities until Phase 4 (Catalog first).
  ```bash
  dotnet ef migrations add InitialCreate --project src/Modules/Catalog/Catalog.Infrastructure --startup-project src/Web/Store.Web --context CatalogDbContext --output-dir Persistence/Migrations
  ```
- **Concurrency**: EF Core shadow rowversion property per aggregate that needs it (ADR-006), not
  a Domain model property. Applied when Inventory gets its first aggregate.
