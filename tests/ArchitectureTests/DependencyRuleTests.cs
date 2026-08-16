using FluentAssertions;

namespace ArchitectureTests;

/// <summary>
/// Enforces Section 2's module boundary rules directly against the project reference graph.
/// Every module name below must match a folder under <c>src/Modules</c> (see
/// <see cref="ProjectGraph"/>) — this list is the modules named in the Phase 1 brief.
/// </summary>
public sealed class DependencyRuleTests
{
    private static readonly string[] Modules =
    [
        "Catalog", "Inventory", "Ordering", "Payments", "Customers",
        "Identity", "Promotions", "Shipping", "Reviews", "Notifications",
    ];

    private readonly IReadOnlyList<ProjectNode> _graph = ProjectGraph.Load();

    [Fact]
    public void Domain_projects_reference_only_SharedKernel()
    {
        foreach (var module in Modules)
        {
            var domain = Find($"{module}.Domain");

            domain.References.Should().BeEquivalentTo(["SharedKernel"],
                because: $"{domain.Name} must not depend on Application, Infrastructure, Web, or any other module");
        }
    }

    [Fact]
    public void Contracts_projects_reference_only_SharedKernel_EventBus_and_Messaging()
    {
        foreach (var module in Modules)
        {
            var contracts = Find($"{module}.Contracts");

            contracts.References.Should().BeSubsetOf(["SharedKernel", "EventBus", "Messaging"],
                because: $"{contracts.Name} is the module's public surface and must stay free of Domain/Application/Infrastructure types. " +
                         "Messaging is allowed here (not just Application) because commands/queries meant for cross-module dispatch " +
                         "(ICommand<T>/IQuery<T>) are defined in Contracts, not Application — see ADR-014.");
        }
    }

    [Fact]
    public void Application_projects_reference_only_sanctioned_building_blocks_own_Domain_and_any_modules_Contracts()
    {
        var sanctionedBuildingBlocks = new[] { "SharedKernel", "EventBus", "Security", "Infrastructure", "Messaging" };

        foreach (var module in Modules)
        {
            var application = Find($"{module}.Application");

            application.References.Should().OnlyContain(
                r => sanctionedBuildingBlocks.Contains(r) || r == $"{module}.Domain" || r.EndsWith(".Contracts", StringComparison.Ordinal),
                because: $"{application.Name} may depend on its own Domain, shared building blocks, and ANY module's *.Contracts " +
                         "(ADR-014 — that's the sanctioned way to call another module synchronously, via IDispatcher + that " +
                         "module's Contracts-defined commands/queries) — never another module's Domain, Application, or " +
                         "Infrastructure directly, and never Web/Worker");
        }
    }

    [Fact]
    public void Infrastructure_projects_reference_only_their_own_module_plus_sanctioned_building_blocks()
    {
        var sanctionedBuildingBlocks = new[] { "SharedKernel", "EventBus", "Observability", "Security", "Infrastructure", "Persistence" };

        foreach (var module in Modules)
        {
            var infrastructure = Find($"{module}.Infrastructure");
            var allowed = sanctionedBuildingBlocks
                .Append($"{module}.Application")
                .Append($"{module}.Domain")
                .Append($"{module}.Contracts");

            infrastructure.References.Should().BeSubsetOf(allowed,
                because: $"{infrastructure.Name} may only depend on its own module's layers and shared building blocks — " +
                         "never another module's projects, and never Web/Worker");
        }
    }

    [Fact]
    public void No_module_references_another_modules_Domain_Application_or_Infrastructure()
    {
        foreach (var module in Modules)
        {
            var otherModulesInternals = Modules
                .Where(m => m != module)
                .SelectMany(m => new[] { $"{m}.Domain", $"{m}.Application", $"{m}.Infrastructure" });

            foreach (var layer in new[] { "Domain", "Application", "Infrastructure", "Contracts" })
            {
                var project = Find($"{module}.{layer}");

                project.References.Should().NotIntersectWith(otherModulesInternals,
                    because: $"{project.Name} must only reach other modules through their *.Contracts project, never their internals");
            }
        }
    }

    [Fact]
    public void SharedKernel_has_no_project_dependencies()
    {
        Find("SharedKernel").References.Should().BeEmpty(
            because: "SharedKernel is the innermost layer — everything depends on it, it depends on nothing");
    }

    [Fact]
    public void No_module_or_building_block_references_Web_or_Worker()
    {
        var webAndWorker = new[] { "Store.Web", "Store.Worker" };

        foreach (var node in _graph.Where(n => n.Name is not ("Store.Web" or "Store.Worker")))
        {
            node.References.Should().NotIntersectWith(webAndWorker,
                because: $"{node.Name} is a lower layer than the composition root and must never depend on it");
        }
    }

    private ProjectNode Find(string name) =>
        _graph.SingleOrDefault(n => n.Name == name)
            ?? throw new InvalidOperationException($"Project '{name}' was not found under src/. Check ProjectGraph discovery.");
}
