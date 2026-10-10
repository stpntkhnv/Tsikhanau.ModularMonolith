using Tsikhanau.Monolith.Scaffold.Files;

namespace Tsikhanau.Monolith.Scaffold.Transactions;

internal sealed record CreatedDirectory(String Path) : JournalEntry(Path)
{
    public override String UndoFailure => $"cannot delete the folder {Path}";

    public override void Undo(IFileSystem fileSystem) => fileSystem.DeleteDirectory(Path);
}
