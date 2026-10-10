namespace Tsikhanau.Monolith.Scaffold.Files;

internal interface IFileSystem
{
    Boolean FileExists(String path);

    Boolean DirectoryExists(String path);

    IReadOnlyList<String> GetFiles(String folder, String searchPattern);

    IReadOnlyList<String> GetDirectories(String folder, String searchPattern);

    Byte[] ReadAllBytes(String path);

    void CreateDirectory(String path);

    void WriteAllBytes(String path, Byte[] content);

    void DeleteFile(String path);

    void DeleteDirectory(String path);

    IDisposable Lock(String path);
}
