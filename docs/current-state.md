Current Phase:
Phase 3 complete (Identity foundation). Next up: Phase 4 (Catalog).

Completed:
- Phase 1: solution scaffold (52 projects), SharedKernel, EventBus/Observability/
  Security/Infrastructure BBs, Store.Web exception handling, ArchitectureTests +
  UnitTests.
- Phase 2: `Persistence` building block — AppDbContextBase, OutboxMessage,
  AuditingInterceptor, soft-delete query filter. Wired into all 10 modules'
  Infrastructure projects. Proven by tests/IntegrationTests (EF Core InMemory).
- Phase 3: Identity module — ApplicationUser/Role, AppIdentityDbContext,
  IIdentityService (Register/Login/Logout/ConfirmEmail/ResetPassword),
  permission-policy registration from Security.Permissions, PermissionRoleSeeder
  (Admin role + permission claims, no hardcoded credentials). Store.Web wired
  (AddIdentityModule, UseAuthentication added — was missing).
- All tests passing: 28 unit + 5 integration + 29 architecture.
- Commits: 71e7f96 (Phase 1), 36008a1 (AGENTS.md/docs).

In Progress:
- (nothing — between phases)

Next:
- Phase 4: Catalog module — Category/Brand/Product/Attribute/ProductVariant
  aggregates, CatalogDbContext (first real use of AppDbContextBase +
  migrations), EF configurations.
- No Account controller/UI yet for Identity (Register/Login views) — needed
  before Identity is user-reachable; do alongside or right after Catalog.

Known Issues:
None.

Important Files:
- AGENTS.md — entry point for any new session.
- docs/architecture.md, docs/modules.md — boundaries, don't re-derive from code.
- docs/database.md — Persistence building block usage pattern for new DbContexts.
- docs/decisions.md — ADR-001..009, check before proposing an architecture change.

Database Changes:
No migrations yet (no module has entities). Connection string placeholder in
src/Web/Store.Web/appsettings.json ("Database", localdb, trusted auth — no
secret).

Decisions Made:
See docs/decisions.md. Newest: ADR-008 (Persistence BB split out to keep EF
Core out of Application), ADR-009 (Identity's user/role types live in
Infrastructure, not Domain).
