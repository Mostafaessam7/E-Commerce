Current Phase:
Phase 6 complete (Inventory). Next up: Phase 7 (Cart) or Phase 11 (Admin) —
whichever the user picks; nothing chosen yet.

Completed:
- Phase 1: solution scaffold (52→55 projects), SharedKernel, EventBus/
  Observability/Security/Infrastructure BBs, Store.Web exception handling.
- Phase 2: `Persistence` BB — AppDbContextBase, OutboxMessage, AuditingInterceptor,
  soft-delete filter.
- Phase 3: Identity module — ApplicationUser/Role, AppIdentityDbContext,
  IIdentityService, permission policies, PermissionRoleSeeder.
- Phase 4: Catalog module — Category/Brand/ProductAttribute/Product
  (variants, images, SEO, tags), CatalogDbContext, Create/GetBySlug/Search
  handlers via the new `Messaging` BB (ADR-010).
- Phase 5: Ecomus template → Razor — _Layout/_Header/_Footer/_ProductCard,
  Home/Shop/Product pages wired to real Catalog queries. wwwroot/ecomus has
  curated assets (~4.4MB, not the full 116MB template image set).
- Phase 6: Inventory module — StockItem (Reserve/Release/Confirm/Receive/
  AdjustTo), StockTransaction history, optimistic concurrency proven under
  real concurrent DbContexts.
- Two real bugs found and fixed via live browser + integration testing (not
  hypothetical — see docs/decisions.md ADR-011/012/013):
  - Per-module DbContexts collided on table names in the shared DB → SQL
    schema per module.
  - New child entities added to already-tracked (loaded) aggregates were
    misclassified Modified instead of Added → `ValueGenerated.Never` on every
    domain-assigned Guid key, applied once for all modules.
  - Slug value-object `==` broke EF query translation → removed VO operator
    overloads.
  - `GlobalExceptionHandler` (Singleton) was constructor-injecting a Scoped
    service (`ICorrelationIdProvider`) → resolved from `HttpContext.RequestServices`
    instead. Also `EF projection into a DTO record with nested collection
    .Select()s` crashed EF's shaper → load via `Include` + map in C# instead.
- All tests passing: 48 unit + 7 integration (includes 2 real-DB Catalog/
  Inventory flow tests) + 29 architecture.
- Commits: 71e7f96, 36008a1, c9f75b6 (see git log for Phase 4/5/6 commit once made).

In Progress:
- (nothing — between phases)

Next:
- No Account controller/UI for Identity yet (Register/Login views) — needed
  before a user can actually reach a protected page.
- No Admin UI yet — Catalog/Inventory can only be exercised via tests/direct
  DB, not through any UI (Phase 11).
- Cart/Checkout (Phase 7-8) needed before Ordering/Payments make sense.
- Categories/Brands have no Application-layer commands yet (only Product does)
  — add when something needs to create/manage them (e.g. Admin panel).

Known Issues:
None outstanding — all found issues this session were fixed and covered by
tests before moving on.

Important Files:
- AGENTS.md — entry point for any new session; has an "EF Core gotchas" section
  worth reading before touching persistence code.
- docs/architecture.md, docs/modules.md — boundaries, don't re-derive from code.
- docs/database.md — schema-per-module + Guid-key convention, read before adding
  a new module's DbContext.
- docs/decisions.md — ADR-001..013.

Database Changes:
Local dev DB `ECommerce` (LocalDB) has 3 applied migrations: CatalogDbContext
(schema `catalog`), AppIdentityDbContext (schema `identity`), InventoryDbContext
(schema `inventory`). Empty (no seed data) — Home/Shop show correct empty
states.

Decisions Made:
See docs/decisions.md. Newest: ADR-010 (Messaging BB), ADR-011 (per-module SQL
schema), ADR-012 (Guid ValueGeneratedNever), ADR-013 (no VO equality operators).
