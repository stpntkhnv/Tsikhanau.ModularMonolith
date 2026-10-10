namespace Tsikhanau.Monolith.Scaffold.Files;

internal sealed class PhysicalFileSystem(String root) : IFileSystem
{
    private readonly String _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    public Boolean FileExists(String path) => File.Exists(ToFullPath(path));

    public Boolean DirectoryExists(String path) => Directory.Exists(ToFullPath(path));

    public IReadOnlyList<String> GetFiles(String folder, String searchPattern) =>
        DirectoryExists(folder)
            ? ToRelativePaths(Directory.GetFiles(ToFullPath(folder), searchPattern))
            : [];

    public IReadOnlyList<String> GetDirectories(String folder, String searchPattern) =>
        DirectoryExists(folder)
            ? ToRelativePaths(Directory.GetDirectories(ToFullPath(folder), searchPattern))
            : [];

    public Byte[] ReadAllBytes(String path) => File.ReadAllBytes(ToFullPath(path));

    public void CreateDirectory(String path) => Directory.CreateDirectory(ToFullPath(path));

    public void WriteAllBytes(String path, Byte[] content) => File.WriteAllBytes(ToFullPath(path), content);

    public void DeleteFile(String path)
    {
        var fullPath = ToFullPath(path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public void DeleteDirectory(String path)
    {
        var fullPath = ToFullPath(path);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }

    private String ToFullPath(String path)
    {
        var fullPath = Path.GetFullPath(path, _root);

        return IsInsideTheApp(fullPath)
            ? fullPath
            : throw new ArgumentException($"{path} is outside the app at {_root}", nameof(path));
    }

    private Boolean IsInsideTheApp(String fullPath) =>
        fullPath == _root || fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private String[] ToRelativePaths(IEnumerable<String> fullPaths) =>
        [
            .. fullPaths
                .Select(fullPath => Path.GetRelativePath(_root, fullPath))
                .Order(StringComparer.Ordinal),
        ];
}
