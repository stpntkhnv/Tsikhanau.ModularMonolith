using Tsikhanau.Monolith.Scaffold.Files;

namespace Tsikhanau.Monolith.Scaffold.Transactions;

internal sealed record ChangedFile(String Path, Byte[] Original) : JournalEntry(Path)
{
    public override String UndoFailure => $"cannot restore {Path}";

    public override void Undo(IFileSystem fileSystem)
    {
        if (!fileSystem.HasContent(Path, Original))
        {
            fileSystem.WriteAllBytes(Path, Original);
        }
    }
}
