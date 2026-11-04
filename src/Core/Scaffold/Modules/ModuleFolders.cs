using Tsikhanau.Monolith.Scaffold.Files;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal static class ModuleFolders
{
    public const String Root = "src/Modules";

    public static IReadOnlyList<String> ReadNames(IFileSystem fileSystem) =>
        [
            .. fileSystem
                .GetDirectories(Root, "*")
                .Select(Path.GetFileName)
                .OfType<String>(),
        ];

    public static String InRoot(String folderName) => $"{Root}/{folderName}";
}
