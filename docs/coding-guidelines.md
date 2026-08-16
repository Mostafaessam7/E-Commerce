# Coding Guidelines

- Nullable reference types on, treated as errors (`Directory.Build.props`).
- Async/await + `CancellationToken` on every I/O-bound method.
- `Result<T>` for expected failures; exceptions only for unreachable invariants
  (see `SharedKernel.Exceptions` XML docs for the exact split).
- Money is always `SharedKernel.ValueObjects.Money`. Never `decimal` alone for
  a price/total, never `double`.
- Aggregate roots: behavior methods only (`order.Cancel(reason)`), no public
  setters for state that has business rules attached.
- Guard clauses (`SharedKernel.Guards.Guard.Against`) for programmer-error
  argument checks; `Result` factory methods for user-triggerable validation.
- Thin controllers: parse request → call handler → map `Result` to
  `IActionResult` via `Store.Web/Infrastructure/ExceptionHandling/ResultExtensions.cs`.
  No business logic in controllers or `.cshtml`.
- No generic repository over EF Core. No new abstraction without a concrete
  current caller.
- Before adding an interface/base class/helper: search the codebase first
  (`SharedKernel`, `BuildingBlocks`) — don't duplicate what exists.
- New module code: put it in the right layer per `docs/architecture.md`; don't
  add a `ProjectReference` that `tests/ArchitectureTests` would reject — run it
  before assuming a layering change is fine.
- Test naming: `Should_condition_when_scenario` or
  `Method_does_X_when_Y` (underscores allowed, CA1707 suppressed in test
  projects only).
- xUnit + FluentAssertions in all test projects.

## After any non-trivial change

```bash
dotnet build ECommerce.slnx
dotnet test tests/UnitTests/UnitTests.csproj
dotnet test tests/ArchitectureTests/ArchitectureTests.csproj
```
Fix compiler/test feedback directly — don't pre-read unrelated files hunting
for hypothetical issues.
