# Database

- One `DbContext` per module (ADR-005), SQL Server, EF Core 10.
- **All modules share one physical database** (`ECommerce`, one connection string) **but each
  gets its own SQL schema** (`catalog`, `inventory`, `identity`, ...) — set via
  `AppDbContextBase.SchemaName` (abstract, override per context) or, for
  `AppIdentityDbContext`, `modelBuilder.HasDefaultSchema("identity")` directly (ADR-011). Without
  this, two modules' identically-named tables (every module gets an `OutboxMessages` table from
  `AppDbContextBase` alone) collide in the shared default `dbo` schema — this happened for real
  when Inventory's first migration hit "there is already an object named 'OutboxMessages'".
- **Every entity's `Guid Id` is assigned client-side in its constructor and mapped
  `ValueGenerated.Never`** (`AppDbContextBase.OnModelCreating` calls
  `ModelBuilderExtensions.MarkDomainAssignedGuidKeysAsNeverGenerated()` automatically for every
  non-owned entity type — nothing to do per-module). Skipping this is a live footgun, not a
  style preference (ADR-012): EF Core's default Added-vs-Unchanged heuristic for a *newly
  discovered* entity with a *non-default* key assumes it already exists in the database. That's
  silently correct when a whole aggregate is `Add()`-ed together (child entities get swept into
  Added via graph fixup), and silently **wrong** the moment a new child entity is attached to an
  *already-tracked* parent that was loaded, not just constructed — EF emits an UPDATE instead of
  an INSERT, which fails with "0 rows affected". Reproduced for real: adding a new
  `StockTransaction` to a loaded `StockItem` inside `Reserve()`.
- **`Persistence` building block** (`src/BuildingBlocks/Persistence`) — the one place EF Core is
  referenced outside a module's own Infrastructure project. Referenced only by `*.Infrastructure`,
  never `*.Application` (ADR-008).
  - `AppDbContextBase` — abstract DbContext base most modules derive from. Gives
    `DbSet<OutboxMessage> OutboxMessages`, auto-applies `IEntityTypeConfiguration<T>`s from the
    derived context's own assembly, sets the module's schema, applies the soft-delete query
    filter and the Guid-key fix above, and exposes a protected
    `EnqueueOutboxMessage(IIntegrationEvent)` helper.
  - `AuditingInterceptor` (`SaveChangesInterceptor`) — stamps `IAuditableEntity` fields. Registered
    via `DbContextOptions.AddInterceptors(...)`, not inheritance, so it also works for contexts
    that can't derive from `AppDbContextBase` (Identity).
  - `ModelBuilderExtensions` — `ApplySoftDeleteQueryFilter()`,
    `MarkDomainAssignedGuidKeysAsNeverGenerated()`.
  - `OutboxMessage`/`OutboxMessageConfiguration` — outbox table shape, identical across modules.
    Write-side only; the Phase 10 `Store.Worker` processor reads it.
- **Value-object query gotcha** (ADR-013): `SharedKernel.ValueObjects.ValueObject` does **not**
  overload `==`/`!=`. A value-converted scalar property (e.g. `Catalog.Domain.ValueObjects.Slug`)
  compared with `==` against another instance of the same VO type must produce an
  `Expression.Equal` node for EF Core to translate it to SQL by applying the converter to both
  sides; an overloaded `==` compiles to a method call instead, which EF cannot translate and
  throws on. Use `.Equals(...)` for value comparisons in C# code — unaffected by this.
- **Design-time factories**: every module with a DbContext has an `IDesignTimeDbContextFactory<T>`
  (e.g. `CatalogDbContextFactory`) so `dotnet ef` doesn't need to build the whole app host —
  add one when a new module gets its first DbContext.
- **Migrations**: one migrations folder per module's Infrastructure project, applied
  independently.
  ```bash
  dotnet ef migrations add InitialCreate --project src/Modules/Catalog/Catalog.Infrastructure --startup-project src/Web/Store.Web --context CatalogDbContext --output-dir Persistence/Migrations
  dotnet ef database update --project src/Modules/Catalog/Catalog.Infrastructure --startup-project src/Web/Store.Web --context CatalogDbContext
  ```
  Currently migrated: `CatalogDbContext`, `AppIdentityDbContext`, `InventoryDbContext`,
  `OrderingDbContext`, `PaymentsDbContext` — every module with a DbContext so far.
- **Concurrency**: EF Core shadow rowversion property per aggregate that needs it (ADR-006), not
  a Domain model property — applied to `StockItem` (`StockItemConfiguration`). Proven by
  `tests/IntegrationTests/Inventory/StockConcurrencyTests.cs`: two DbContexts reserving the same
  last unit — the second `SaveChangesAsync` throws `ConflictException`, exactly one reservation
  persists.
- Connection string placeholder: `src/Web/Store.Web/appsettings.json` → `ConnectionStrings:Database`
  (localdb, trusted auth, no secret).
