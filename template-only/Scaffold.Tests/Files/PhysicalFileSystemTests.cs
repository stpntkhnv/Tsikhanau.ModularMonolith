using Tsikhanau.Monolith.Scaffold.Files;

namespace Tsikhanau.Monolith.Scaffold.Tests.Files;

public sealed class PhysicalFileSystemTests : IDisposable
{
    private readonly DirectoryInfo _parent = Directory.CreateTempSubdirectory("scaffold-files-");

    public void Dispose() => _parent.Delete(recursive: true);

    [Fact]
    public void Paths_are_relative_to_the_app_root()
    {
        var fileSystem = new PhysicalFileSystem(CreateFolder("app"));
        fileSystem.CreateDirectory("src/Modules");
        fileSystem.WriteAllBytes("src/Modules/Notes.txt", [1]);

        var files = fileSystem.GetFiles("src/Modules", "*.txt");

        files.ShouldBe(["src/Modules/Notes.txt"]);
    }

    [Theory]
    [InlineData("../neighbour")]
    [InlineData("src/../../neighbour")]
    public void Folder_outside_the_app_is_never_deleted(String path)
    {
        var neighbour = CreateFolder("neighbour");
        var fileSystem = new PhysicalFileSystem(CreateFolder("app"));

        var exception = Should.Throw<ArgumentException>(() => fileSystem.DeleteDirectory(path));

        var hasNeighbour = Directory.Exists(neighbour);

        exception.Message.ShouldStartWith($"{path} is outside the app", Case.Sensitive);
        hasNeighbour.ShouldBeTrue();
    }

    [Fact]
    public void Rooted_path_is_refused()
    {
        var fileSystem = new PhysicalFileSystem(CreateFolder("app"));
        var neighbour = CreateFolder("neighbour");

        var exception = Should.Throw<ArgumentException>(() => fileSystem.DeleteDirectory(neighbour));

        var hasNeighbour = Directory.Exists(neighbour);

        exception.Message.ShouldStartWith($"{neighbour} is outside the app", Case.Sensitive);
        hasNeighbour.ShouldBeTrue();
    }

    private String CreateFolder(String name) =>
        _parent
            .CreateSubdirectory(name)
            .FullName;
}
