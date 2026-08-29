# E-Commerce

Enterprise e-commerce platform: ASP.NET Core MVC (Razor) storefront + admin panel, built as a
modular monolith on .NET 10, EF Core, SQL Server, and Redis. The storefront UI is a ThemeForest
"Ecomus" HTML template converted to Razor views (curated asset subset, not the raw package).

## Status

All ten modules (Catalog, Inventory, Ordering, Payments, Identity, Notifications, Customers,
Promotions, Shipping, Reviews) have real Domain/Application/Infrastructure code. Storefront,
admin panel, checkout, payments (fake gateway with real webhook mechanics), Redis caching,
CI (build/test/image publish), rate limiting, sitemap/robots.txt, a full design-system redesign
with dark mode, and Arabic/English localization (including the admin area) are all implemented.
Start at `PROJECT-STATUS.md` — what is closed, which decisions are adopted, what is still open,
known debt, and what was deferred on purpose. For the full phase-by-phase account behind it, see
`docs/current-state.md`; for the reasoning behind individual choices, `docs/decisions.md`.

## Architecture

Modular monolith, DDD-lite, Clean Architecture layering per module, CQRS where it earns its
keep, Domain Events (in-process) + Integration Events (cross-module, via a transactional Outbox).
No microservices. Full rationale: `docs/architecture.md`; per-module detail: `docs/modules.md`;
the decision log: `docs/decisions.md`.

```
src/BuildingBlocks/  SharedKernel, EventBus, Observability, Security, Infrastructure, Persistence, Messaging, Caching
src/Modules/{Catalog,Inventory,Ordering,Payments,Customers,Identity,Promotions,Shipping,Reviews,Notifications}/
    each: *.Domain / *.Application / *.Infrastructure / *.Contracts
src/Web/Store.Web       composition root, MVC storefront + admin area
src/Workers/Store.Worker  background host (Outbox processor)
tests/{UnitTests,IntegrationTests,ArchitectureTests,EndToEndTests}
```

## Getting started

### Local (LocalDB)

```bash
dotnet restore ECommerce.slnx
dotnet build ECommerce.slnx
```

Apply migrations per module (see `docs/database.md` for the full list and `dotnet ef` commands),
then run:

```bash
dotnet run --project src/Web/Store.Web
dotnet run --project src/Workers/Store.Worker
```

### Docker Compose

```bash
cp .env.example .env
# edit .env — set a real SQL_SA_PASSWORD
docker compose up --build
```

Brings up SQL Server, Redis, `store-web` (http://localhost:8080, `/health`), and `store-worker`.
See `docs/deployment.md`. Note: a real `docker compose up --build` against a live Docker daemon
has not been verified in this repo's development sandbox — see `docs/current-state.md`'s
"Known Issues".

## Testing

```bash
dotnet test tests/UnitTests/UnitTests.csproj
dotnet test tests/ArchitectureTests/ArchitectureTests.csproj
dotnet test tests/IntegrationTests/IntegrationTests.csproj      # needs LocalDB with migrations applied
dotnet test tests/EndToEndTests/EndToEndTests.csproj            # needs the same DB as IntegrationTests
```

`dotnet test` does not accept multiple `.csproj` arguments — run one project at a time. Details:
`docs/testing.md`.

## CI/CD

GitHub Actions (`.github/workflows/build-test.yml`) builds, runs all four test suites against a
real LocalDB instance, and (on push to `main`) publishes both Docker images to GHCR. Details:
`docs/ci-cd.md`.

## Documentation map

Start with `AGENTS.md`, then `docs/current-state.md`. Full docs index and per-file contents are
listed in `AGENTS.md`'s "Docs map" table.

`ecomus-package/` is the raw third-party ThemeForest template source — reference material, not
part of the application (only a curated subset under `wwwroot/ecomus` and `wwwroot/admin-ecomus`
is actually served).
