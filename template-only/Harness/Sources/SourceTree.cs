using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Sources;

internal sealed class SourceTree(String root)
{
    private const Int32 ShownAnchorLength = 60;

    public String Root => root;

    public Result<Unit> Replace(String path, String anchor, String replacement) =>
        Read(path)
            .Ensure(text => CountOf(anchor, text) == 1, text => AnchorNotFoundOnce(path, anchor, text))
            .Bind(text => Write(path, text.Replace(anchor, replacement, StringComparison.Ordinal)));

    public Result<Unit> Write(String path, String text)
    {
        File.WriteAllText(FullPath(path), text);
        return Unit.Value;
    }

    public Result<String> Read(String path) =>
        File.Exists(FullPath(path))
            ? File.ReadAllText(FullPath(path))
            : Error.NotFound("harness.file", $"no file {path}");

    public IReadOnlyList<String> FindFiles(String folder, String pattern) =>
        [
            .. Directory
                .GetFiles(FullPath(folder), pattern)
                .Select(file => Path.GetRelativePath(root, file))
                .Order(StringComparer.Ordinal),
        ];

    private String FullPath(String path) => Path.Combine(root, path);

    private static Int32 CountOf(String anchor, String text) => text.Split(anchor).Length - 1;

    private static Error AnchorNotFoundOnce(String path, String anchor, String text)
    {
        var shownAnchor = anchor[..Math.Min(anchor.Length, ShownAnchorLength)];

        return Error.Failure(
            "harness.anchor",
            $"pattern found {CountOf(anchor, text)} times, expected once, in {path}: {shownAnchor}");
    }
}
