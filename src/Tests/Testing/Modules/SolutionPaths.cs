namespace ModularMonolith.Testing.Modules;

public static class SolutionPaths
{
    public static readonly String Root = LocateRoot(new DirectoryInfo(AppContext.BaseDirectory));

    public static readonly String Modules = Path.Combine(Root, "src", "Modules");

    private static String LocateRoot(DirectoryInfo directory) =>
        File.Exists(Path.Combine(directory.FullName, "global.json"))
            ? directory.FullName
            : LocateRoot(
                directory.Parent
                    ?? throw new InvalidOperationException("global.json was not found above the test output"));
}
