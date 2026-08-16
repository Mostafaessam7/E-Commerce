# CI/CD

## `.github/workflows/build-test.yml`

Triggers on every PR into `main`/`master` and every push to `main`/`master`. One job,
`windows-latest`, sequential steps — no matrix, no parallel jobs (see ADR-024 for why
`windows-latest` specifically). A second push to the same PR cancels the still-running check for
the first (`concurrency` block) — no point finishing a run whose result nobody will look at.

Steps, in order:

1. `actions/checkout` + `actions/setup-dotnet` (`10.0.x`).
2. `dotnet restore ECommerce.slnx` → `dotnet build ECommerce.slnx --configuration Release`.
3. **Unit + Architecture tests** run immediately after the build — no database needed, fail fast
   before spending time on LocalDB setup if either project itself is broken.
4. `SqlLocalDB start MSSQLLocalDB` — GitHub's `windows-latest` runner image ships this
   preinstalled; this just makes sure the instance is running.
5. `dotnet tool install --global dotnet-ef --version 10.0.11` — pinned to match the
   `Microsoft.EntityFrameworkCore*` versions in `Directory.Packages.props` exactly (a mismatched
   tool version only prints a warning, not an error, but keeping them in lockstep avoids the
   warning noise and any edge-case behavior drift).
6. One `dotnet ef database update` per module context (Catalog/Inventory/Ordering/Payments/
   Identity) — the exact same commands documented in `docs/database.md` for a developer setting
   up a fresh clone, just scripted. Creates the `ECommerce` database on the runner's LocalDB
   instance and applies every migration, because a fresh runner obviously doesn't have the schema
   local dev machines accumulate over time.
7. **Integration tests** — now that the schema exists, `IntegrationTests`' hardcoded
   `Server=(localdb)\mssqllocaldb;Database=ECommerce;...` connection string (docs/testing.md) just
   works, unmodified.

## Not yet built

- No image publish step — Phase 13's Dockerfiles aren't built/pushed by this workflow yet (the
  phase was scoped to "build+test on PR" specifically, not a release pipeline). Adding a
  `docker build` validation job (or push-to-registry on a tag) is a natural next step, not done
  here to keep this phase's scope match what was asked.
- No NuGet restore caching (`actions/cache` / `setup-dotnet`'s built-in cache needs a
  `packages-lock.json` this repo doesn't generate) — every run restores from scratch. Fine at this
  project's size; revisit if CI time becomes annoying.
- No branch protection rule requiring this check to pass before merge — that's a GitHub repo
  setting, not something a workflow file can express; turn it on in the repo's Settings once this
  workflow has a remote to run against.
- No automatic dependency/vulnerability scanning (Dependabot, `dotnet list package
  --vulnerable`) beyond the one-time manual pins already in `Directory.Packages.props`
  (docs/decisions.md ADR mentions e.g. the `System.Security.Cryptography.Xml` pin).
