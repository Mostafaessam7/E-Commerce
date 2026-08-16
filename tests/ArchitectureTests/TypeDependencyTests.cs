using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace ArchitectureTests;

/// <summary>
/// IL-level checks on top of <see cref="DependencyRuleTests"/>'s project-graph checks. These
/// catch a violation even if it sneaks in through a NuGet package rather than a
/// <c>ProjectReference</c> (e.g. someone adding <c>Microsoft.EntityFrameworkCore</c> straight to
/// a Domain project's <c>.csproj</c>) — something the project-graph test can't see. Rules that
/// target module Domain/Application assemblies are forward-looking: those assemblies have no
/// types yet in Phase 1, so the checks pass vacuously today and start earning their keep from
/// Phase 2 onward without any changes needed here.
/// </summary>
public sealed class TypeDependencyTests
{
    private static readonly string[] Modules =
    [
        "Catalog", "Inventory", "Ordering", "Payments", "Customers",
        "Identity", "Promotions", "Shipping", "Reviews", "Notifications",
    ];

    [Fact]
    public void SharedKernel_does_not_depend_on_web_or_data_frameworks()
    {
        AssertNoForbiddenDependency(typeof(SharedKernel.Primitives.Entity<>).Assembly);
    }

    [Fact]
    public void EventBus_does_not_depend_on_web_or_data_frameworks()
    {
        AssertNoForbiddenDependency(typeof(EventBus.IIntegrationEvent).Assembly);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_assembly_does_not_depend_on_web_or_data_frameworks(string module)
    {
        AssertNoForbiddenDependency(Assembly.Load($"{module}.Domain"));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Application_assembly_does_not_depend_on_aspnetcore_mvc_or_efcore(string module)
    {
        var result = Types.InAssembly(Assembly.Load($"{module}.Application"))
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore.Mvc")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"{module}.Application is business logic — it must stay runnable outside ASP.NET Core and independent of the EF Core provider. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    public static TheoryData<string> ModuleNames() => new(Modules);

    private static void AssertNoForbiddenDependency(Assembly assembly)
    {
        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore.Mvc",
                "Microsoft.AspNetCore.Http")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"{assembly.GetName().Name} is a low-level building block/domain assembly and must not depend on web or ORM frameworks. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
