namespace Tsikhanau.Monolith.Scaffold.Tests.Support;

internal static class TemplateFolder
{
    private const String NoTemplateRoot = "no folder above the test output holds global.json";

    public static String Root { get; } = FindRoot(new DirectoryInfo(AppContext.BaseDirectory));

    private static String FindRoot(DirectoryInfo folder) =>
        File.Exists(Path.Combine(folder.FullName, "global.json"))
            ? folder.FullName
            : FindRoot(folder.Parent ?? throw new InvalidOperationException(NoTemplateRoot));
}
