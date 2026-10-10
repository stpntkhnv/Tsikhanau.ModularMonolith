using Tsikhanau.Monolith.Scaffold.Files;

namespace Tsikhanau.Monolith.Scaffold.Tests.Support;

internal sealed class FailingFileSystem(
    String root,
    Int32 failingWrite = 0,
    String failingFolder = "",
    String failingRestore = "",
    Boolean writesPartially = true)
    : IFileSystem
{
    private readonly PhysicalFileSystem _fileSystem = new(root);

    private readonly HashSet<String> _writtenPaths = new(StringComparer.Ordinal);

    private Int32 _writes;

    public Boolean FileExists(String path) => _fileSystem.FileExists(path);

    public Boolean DirectoryExists(String path) => _fileSystem.DirectoryExists(path);

    public IReadOnlyList<String> GetFiles(String folder, String searchPattern) =>
        _fileSystem.GetFiles(folder, searchPattern);

    public IReadOnlyList<String> GetDirectories(String folder, String searchPattern) =>
        _fileSystem.GetDirectories(folder, searchPattern);

    public Byte[] ReadAllBytes(String path) => _fileSystem.ReadAllBytes(path);

    public void CreateDirectory(String path)
    {
        if (path == failingFolder)
        {
            throw new UnauthorizedAccessException("read-only folder");
        }

        _fileSystem.CreateDirectory(path);
    }

    public void WriteAllBytes(String path, Byte[] content)
    {
        var isRestore = !_writtenPaths.Add(path);
        if (isRestore && path == failingRestore)
        {
            throw new IOException("still read-only");
        }

        if (++_writes == failingWrite)
        {
            WritePartially(path, content);
            throw new IOException("disk full");
        }

        _fileSystem.WriteAllBytes(path, content);
    }

    public void DeleteFile(String path) => _fileSystem.DeleteFile(path);

    public void DeleteDirectory(String path) => _fileSystem.DeleteDirectory(path);

    private void WritePartially(String path, Byte[] content)
    {
        if (writesPartially)
        {
            _fileSystem.WriteAllBytes(path, content[..(content.Length / 2)]);
        }
    }
}
