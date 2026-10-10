using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Files;

internal static class FileSystemExtensions
{
    extension(IFileSystem fileSystem)
    {
        public Result<TextFile> ReadTextFile(String path) =>
            fileSystem.FileExists(path)
                ? fileSystem
                    .Attempt(() => fileSystem.ReadAllBytes(path), $"cannot read {path}")
                    .Map(content => new TextFile(path, content))
                : Error.Failure("scaffold.missing-file", $"{path} does not exist");

        public Boolean HasContent(String path, Byte[] content) =>
            fileSystem
                .Attempt(() => fileSystem.ReadAllBytes(path), $"cannot read {path}")
                .Match(currentContent => currentContent.SequenceEqual(content), _ => false);

        public Result<T> Attempt<T>(Func<T> operation, String failure)
            where T : notnull
        {
            try
            {
                return operation();
            }
            catch (IOException exception)
            {
                return Error.Failure("scaffold.io", $"{failure}: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                return Error.Failure("scaffold.io", $"{failure}: {exception.Message}");
            }
        }

        public Result<Unit> Attempt(Action operation, String failure) =>
            fileSystem.Attempt(
                () =>
                {
                    operation();
                    return Unit.Value;
                },
                failure);
    }
}
