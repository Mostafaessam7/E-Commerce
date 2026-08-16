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
- **EndToEndTests**: full flows (register → cart → checkout → payment →
  order). Not populated yet.

Run one project at a time — `dotnet test` rejects multiple `.csproj` args:

```bash
dotnet test tests/UnitTests/UnitTests.csproj
dotnet test tests/ArchitectureTests/ArchitectureTests.csproj
dotnet test tests/IntegrationTests/IntegrationTests.csproj
```
