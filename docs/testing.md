# Testing

- **UnitTests**: Domain rules, value objects, Result pattern, guards. No I/O.
  References only the projects under test — add a `ProjectReference` when a
  module gets domain code to test.
- **ArchitectureTests**: `DependencyRuleTests.cs` checks the `.csproj`
  reference graph directly (works even for modules with no code yet).
  `TypeDependencyTests.cs` adds NetArchTest IL-level checks (forward-looking
  for module Domain/Application assemblies, real today for BuildingBlocks).
  Run this after changing any `ProjectReference`.
- **IntegrationTests**: EF Core against a real/testcontainer SQL Server,
  Outbox, command/query handlers touching the DB. Not populated yet (needs
  Phase 2).
- **EndToEndTests**: full flows (register → cart → checkout → payment →
  order). Not populated yet (needs Phase 7+).

Run one project at a time — `dotnet test` rejects multiple `.csproj` args:

```bash
dotnet test tests/UnitTests/UnitTests.csproj
dotnet test tests/ArchitectureTests/ArchitectureTests.csproj
```
