using System.Xml.Linq;

namespace ArchitectureTests;

/// <summary>
/// A parsed <c>&lt;ProjectReference&gt;</c> graph over every <c>.csproj</c> under <c>src/</c>.
/// Deterministic and independent of whether a module has any code in it yet — which matters in
/// Phase 1, where most modules are still empty class libraries with no types for an IL-based
/// tool (NetArchTest) to inspect. Once modules have real code, <see cref="TypeDependencyTests"/>
/// adds the IL-level checks on top of this project-level enforcement; neither replaces the other.
/// </summary>
internal sealed record ProjectNode(string Name, string Path, IReadOnlyList<string> References);

internal static class ProjectGraph
{
    public static IReadOnlyList<ProjectNode> Load()
    {
        var srcRoot = System.IO.Path.Combine(SolutionRoot.Path, "src");
        var projectFiles = Directory.EnumerateFiles(srcRoot, "*.csproj", SearchOption.AllDirectories);

        var nodes = new List<ProjectNode>();

        foreach (var projectFile in projectFiles)
        {
            var doc = XDocument.Load(projectFile);
            var directory = System.IO.Path.GetDirectoryName(projectFile)!;

            var references = doc
                .Descendants("ProjectReference")
                .Select(e => e.Attribute("Include")!.Value)
                .Select(relative => System.IO.Path.GetFileNameWithoutExtension(
                    System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, relative))))
                .ToList();

            nodes.Add(new ProjectNode(
                Name: System.IO.Path.GetFileNameWithoutExtension(projectFile),
                Path: projectFile,
                References: references));
        }

        return nodes;
    }
}
