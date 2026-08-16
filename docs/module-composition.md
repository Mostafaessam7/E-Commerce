# Module composition: explicit registration, not a discovered `IModule`

## Decision

Each module's `*.Infrastructure` project exposes one DI extension method:

```csharp
// Catalog.Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("Database")));
        // ... module-internal registrations (repositories, domain services, handlers)
        return services;
    }
}
```

`src/Web/Store.Web/Program.cs` calls every module's extension explicitly, in one visible block:

```csharp
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
// ...
```

`src/Workers/Store.Worker/Program.cs` does the same for whichever modules the worker needs
(starting with the Outbox processor's dependencies in Phase 10).

## Why not an `IModule` interface + assembly-scanning discovery?

An earlier draft considered a `BuildingBlocks.Infrastructure.IModule` interface
(`string Name`, `void Register(IServiceCollection, IConfiguration)`) discovered at startup by
scanning loaded assemblies for implementations. Rejected:

- **The module list is closed and known at compile time.** This is a modular monolith with ten
  fixed modules, not a plugin host that loads third-party assemblies dynamically. Reflection-based
  discovery solves a problem ("what modules exist?") that a single explicit list already answers
  more simply.
- **Explicit registration is the actual architecture diagram.** Reading `Program.cs` top to bottom
  tells a new engineer exactly which modules are wired into this deployable and in what order —
  that's real, load-bearing documentation. A scanning mechanism hides the same information behind
  "whatever assemblies happened to be in the output folder," which is harder to reason about and
  harder to unit test in isolation.
- **It's strictly worse for debugging.** A missing or misordered registration is a compile-visible
  line to fix, not a silent runtime discovery failure.

This is the "avoid overengineering — don't add abstraction without a clear reason" rule (Section 35)
applied directly: an `IModule` abstraction would exist to solve a discovery problem this project
doesn't have.

## What if a module needs to expose HTTP endpoints directly (not just services)?

Same pattern, one more extension method per module if/when needed:
`public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)`,
called from Program.cs next to the route configuration. Not added in Phase 1 because no module has
endpoints yet.
