# Deployment (Docker)

## Running the stack

```bash
cp .env.example .env
# edit .env — set a real SQL_SA_PASSWORD (SQL Server's own complexity policy: 8+ chars, upper+lower+digit/symbol)
docker compose up --build
```

- `store-web`: http://localhost:8080 (storefront + `/Admin` + `GET /health`)
- `sqlserver`: `localhost:1433` (SA login, password from `.env`)
- `redis`: `localhost:6379`

`.env` is gitignored (real credential) — `.env.example` is the tracked template. `docker-compose.yml`
requires `SQL_SA_PASSWORD` explicitly (`${SQL_SA_PASSWORD:?...}`, no hardcoded default) — same
"never commit a real credential" discipline as everywhere else in this repo (docs/security.md).

## Images

- `src/Web/Store.Web/Dockerfile`, `src/Workers/Store.Worker/Dockerfile`: multi-stage
  (`sdk` build → `aspnet`/`runtime`). **Build context is the repo root**, not the project folder —
  Central Package Management (`Directory.Packages.props`) and `Directory.Build.props` live there,
  and these projects reference most of `src/`. `dotnet restore <single csproj>` (not the `.slnx`)
  pulls in only what that project actually references — `tests/` is never touched, so it's excluded
  from the build context entirely (`.dockerignore`).
- `Store.Web` uses the `aspnet` runtime image (needs the ASP.NET Core shared framework);
  `Store.Worker` uses the plain `runtime` image (no inbound HTTP — docs/observability.md). Both use
  the Debian-based tag, not `-alpine`: `Directory.Build.props` sets `InvariantGlobalization=false`
  for Arabic+English localization, which needs ICU data Alpine strips out by default.
- `.dockerignore` excludes `ecomus-package/`/`Mecodex-Brand-Assets/` (175MB+ of raw ThemeForest
  template source, not referenced by any project — only the curated subset already committed
  under `src/Web/Store.Web/wwwroot/ecomus` is served at runtime, see ADR (Phase 5)).

## Database migrations in the container

Local dev (`dotnet run` against LocalDB) still uses the normal `dotnet ef database update` per
context workflow (docs/database.md) — nothing about that changed. Inside Docker Compose, there's
no interactive step to run migrations manually, so both `Program.cs` files gained an opt-in block:

```csharp
if (app.Configuration.GetValue<bool>("ApplyMigrationsOnStartup")) { ... }
```

guarded by the `ApplyMigrationsOnStartup` config key, which `docker-compose.yml` sets to `"true"`
via environment variable and which is otherwise never set (defaults to `false`). Each composition
root migrates only the `DbContext`s it already wires (`Store.Web`: all 5; `Store.Worker`:
Ordering + Payments) via `Persistence.MigrationExtensions.MigrateWithRetryAsync<TContext>` — retries
a few times with a delay, because the `sqlserver` container's health check can pass a moment before
it's actually ready to accept every connection. `store-worker` `depends_on` `store-web`, but its own
migration call doesn't assume that ordering held (safe to run twice against the same schema).

## Redis

`docker-compose.yml`'s `redis` container backs a real read-through cache as of Phase 22 —
`Caching.AddDistributedCaching` (`BuildingBlocks/Caching`) registers `AddStackExchangeRedisCache`
against `ConnectionStrings:Redis`, which `docker-compose.yml`'s `store-web` service sets to
`redis:6379`. `Catalog.Infrastructure`'s `CachedProductQueries` decorator is the actual reader —
the storefront's product-detail-page and search/listing queries, TTL-only (60s/30s), never the
checkout price/stock re-validation query or admin listings. See ADR-033 for the full reasoning.
Local `dotnet run` against LocalDB with no Redis container running still works — `AddCatalogModule`
falls back to `AddDistributedMemoryCache` when nothing already registered `IDistributedCache` (same
opt-in-without-hard-dependency posture as `ApplyMigrationsOnStartup`/`AdminUserBootstrapper`).

## Not yet built

Build+test-on-PR exists (`docs/ci-cd.md`, Phase 14) — pushing these images to a registry does
not; that's still a manual `docker compose build`. Also missing: a production-shaped compose file
(secrets manager instead of `.env`, no local SQL Server container, HTTPS termination), and a
health-check-aware `HEALTHCHECK` instruction in the app Dockerfiles (skipped — the `aspnet`/
`runtime` base images don't ship `curl`/`wget`, and installing one just for a `HEALTHCHECK`
directive wasn't worth the extra image layer at this stage; `docker-compose.yml`'s `depends_on`
already gates on `sqlserver`'s own health check).
