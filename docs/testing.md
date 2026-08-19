# Testing

- **UnitTests**: Domain rules, value objects, Result pattern, guards. No I/O.
  References only the projects under test — add a `ProjectReference` when a
  module gets domain code to test.
- **ArchitectureTests**: `DependencyRuleTests.cs` checks the `.csproj`
  reference graph directly (works even for modules with no code yet).
  `TypeDependencyTests.cs` adds NetArchTest IL-level checks (forward-looking
  for module Domain/Application assemblies, real today for BuildingBlocks).
  Run this after changing any `ProjectReference`.
- **IntegrationTests**: EF Core against a real LocalDB SQL Server instance
  (one shared, mutable DB — not testcontainers), Outbox, command/query
  handlers touching the DB, composed via a plain `ServiceCollection` (module
  `Add*Module()` calls, same as Store.Web but without the MVC host). Runs
  **sequentially, not in parallel** — `xunit.runner.json` disables
  `parallelizeAssembly`/`parallelizeTestCollections`; parallel test classes
  against the one shared DB produced a real, reproducible spurious
  `DbUpdateConcurrencyException` (ADR-019). If you add a new integration test
  project, copy this file and the csproj's `CopyToOutputDirectory` item too.
- **EndToEndTests** (Phase 25): full flows (register → confirm email → log in → cart → checkout →
  payment → order), driven over **real HTTP** via `Microsoft.AspNetCore.Mvc.Testing`'s
  `WebApplicationFactory<Program>` — real MVC pipeline, real Razor-rendered forms, real antiforgery
  tokens scraped out of the actual HTML, real cookie-based cart/auth identity, real confirmation
  links pulled out of the real `NotificationLog` row. This is deliberately a different mechanism
  than IntegrationTests' plain `ServiceCollection` composition — it proves a browser-driven journey
  works, not just that the handlers behind it do. `Store.Web/Program.cs` has a trailing
  `public partial class Program;` marker so `WebApplicationFactory<Program>` can reference it from
  another assembly (top-level statements otherwise generate an `internal` one) — a standard ASP.NET
  Core testing pattern, not a functional change. One gotcha worth remembering:
  `AnonymousIdExtensions`' cart-identity cookie and the auth cookie are both `Secure` — the test
  client's `BaseAddress` must be `https://localhost` (`WebApplicationFactoryClientOptions`), or
  `CreateClient()`'s default `http://` base silently never sends them back, and every request
  looks like a brand new anonymous visitor.

Run one project at a time — `dotnet test` rejects multiple `.csproj` args:

```bash
dotnet test tests/UnitTests/UnitTests.csproj
dotnet test tests/ArchitectureTests/ArchitectureTests.csproj
dotnet test tests/IntegrationTests/IntegrationTests.csproj
dotnet test tests/EndToEndTests/EndToEndTests.csproj
```

## CI (Phase 14, EndToEndTests added Phase 25)

`.github/workflows/build-test.yml` runs all four on every PR — on `windows-latest` specifically,
because `IntegrationTests`'/`EndToEndTests`' hardcoded LocalDB connection strings need Windows'
actual LocalDB feature, which that runner image ships preinstalled (ADR-024, docs/ci-cd.md). It
applies migrations first (same `dotnet ef database update` commands as docs/database.md) since a
fresh runner has no schema yet — don't skip that step when replicating the workflow locally.
`EndToEndTests` runs last, reusing the same already-migrated database Integration tests just used —
no separate migration step of its own.
