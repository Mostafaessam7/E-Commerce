# CI/CD

## `.github/workflows/build-test.yml`

Triggers on every PR into `main`/`master` and every push to `main`/`master`. Two jobs:
`build-and-test` (`windows-latest`, sequential steps — no matrix, no parallel jobs, see ADR-024
for why `windows-latest` specifically) and `publish-images` (Phase 23 — `ubuntu-latest`, only on
a push to `main`/`master`, gated on `build-and-test` passing first). A second push to the same PR
cancels the still-running check for the first (`concurrency` block) — no point finishing a run
whose result nobody will look at.

### `build-and-test`

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
6. One `dotnet ef database update` per module context — Catalog/Inventory/Ordering/Payments/
   Identity/Notifications/Customers/Promotions/Shipping/Reviews, all ten migrated contexts as of
   Phase 20 — the exact same commands documented in `docs/database.md` for a developer setting up
   a fresh clone, just scripted. Creates the `ECommerce` database on the runner's LocalDB instance
   and applies every migration, because a fresh runner obviously doesn't have the schema local dev
   machines accumulate over time. **Keep this list in sync** with every module that gains a real
   `Infrastructure` project — a missing context here doesn't fail loudly at this step, it fails
   later in Integration tests with a confusing "invalid object name" from whichever table that
   context owns.
7. **Integration tests** — now that the schema exists, `IntegrationTests`' hardcoded
   `Server=(localdb)\mssqllocaldb;Database=ECommerce;...` connection string (docs/testing.md) just
   works, unmodified.
8. **End-to-end tests** (Phase 25) — `EndToEndTests` boots the real `Store.Web` host in-memory
   (`WebApplicationFactory<Program>`) and drives it over real HTTP against the same already-migrated
   database; no separate migration step needed for it.

### `publish-images` (Phase 23)

Builds and pushes both application images to GHCR (`ghcr.io/<owner>/<repo>-store-web` and
`...-store-worker`), each tagged with the commit SHA and `latest`. Runs on `ubuntu-latest` (image
builds need a Linux Docker daemon; `build-and-test` stays on `windows-latest` for LocalDB) using
`docker/build-push-action`, authenticated via the workflow's own `GITHUB_TOKEN` — no extra secret
to configure, `packages: write` permission is enough to push to the repo's own GHCR namespace.
Guarded by `if: github.event_name == 'push' && ...main/master` so a PR (including one from a fork,
which wouldn't have package-write permission anyway) never attempts a push, only builds.

**Not independently verified against a real Docker daemon** — this sandbox's Docker Desktop
backend process exits within ~15 seconds of launch (nested virtualization unavailable), a real
attempt made in this session, not just an assumption carried over from Phase 13. What *was*
verified without a daemon: `docker compose config` parses and correctly interpolates
`docker-compose.yml` (including Phase 22's new `ConnectionStrings__Redis` wiring); the workflow
YAML itself parses correctly (job/step structure, multi-line `tags:` strings); and — the strongest
substitute for an actual `docker build` — running the exact `dotnet restore`/`dotnet publish`
commands each Dockerfile's `RUN` steps execute, against a byte-for-byte copy of the Dockerfiles'
own build context (repo root, `.dockerignore`'s `bin/`/`obj/`/`tests/` exclusions applied
manually), succeeded for both `Store.Web.csproj` and `Store.Worker.csproj` and produced the exact
`Store.Web.dll`/`Store.Worker.dll` each Dockerfile's `ENTRYPOINT` expects. The only step actually
unverified is the container-runtime layer itself (`FROM mcr.microsoft.com/dotnet/aspnet:10.0`,
`COPY --from=build`) — a real `docker compose up --build` pass is still worth doing in an
environment where Docker Desktop can actually start.

## Not yet built

- No NuGet restore caching (`actions/cache` / `setup-dotnet`'s built-in cache needs a
  `packages-lock.json` this repo doesn't generate) — every run restores from scratch. Fine at this
  project's size; revisit if CI time becomes annoying.
- ~~No branch protection rule~~ — now live. Classic branch protection (and the newer repository
  rulesets) both require GitHub Pro for a *private* repo; once the repo owner made
  `github.com/Mostafaessam7/E-Commerce` public, a plain `gh api` call turned it on:
  ```
  gh api -X PUT repos/Mostafaessam7/E-Commerce/branches/main/protection \
    -H "Accept: application/vnd.github+json" \
    --input - <<'EOF'
  {
    "required_status_checks": { "strict": true, "contexts": ["build-and-test"] },
    "enforce_admins": false,
    "required_pull_request_reviews": null,
    "restrictions": null,
    "allow_force_pushes": false,
    "allow_deletions": false
  }
  EOF
  ```
  `main` now requires the `build-and-test` check to pass (and to be up to date with the base branch
  — `strict: true`) before a merge is allowed; force-pushes and branch deletion are blocked too.
  `enforce_admins: false` so the repo owner can still push directly when needed. Verify with
  `gh api repos/.../branches/main/protection`.
- No automatic dependency/vulnerability scanning (Dependabot, `dotnet list package
  --vulnerable`) beyond the one-time manual pins already in `Directory.Packages.props`
  (docs/decisions.md ADR mentions e.g. the `System.Security.Cryptography.Xml` pin).
