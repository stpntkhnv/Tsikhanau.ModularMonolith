namespace ModularMonolith.Testing.Modules;

public static class SolutionPaths
{
    public static readonly String Root = LocateRoot(new DirectoryInfo(AppContext.BaseDirectory));

    public static readonly String Modules = Path.Combine(Root, "src", "Modules");

    private static String LocateRoot(DirectoryInfo directory) =>
        File.Exists(Path.Combine(directory.FullName, "ModularMonolith.slnx"))
            ? directory.FullName
            : LocateRoot(
                directory.Parent
                    ?? throw new InvalidOperationException("ModularMonolith.slnx was not found above the test output"));
}
