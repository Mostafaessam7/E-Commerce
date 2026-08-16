namespace ArchitectureTests;

/// <summary>Locates the repo root from wherever the test assembly happens to run.</summary>
internal static class SolutionRoot
{
    public static string Path { get; } = Find();

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "ECommerce.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate ECommerce.slnx above " + AppContext.BaseDirectory);
    }
}
