using Tsikhanau.Monolith.Scaffold.Files;

namespace Tsikhanau.Monolith.Scaffold.Transactions;

internal sealed record CreatedFile(String Path) : JournalEntry(Path)
{
    public override String UndoFailure => $"cannot delete {Path}";

    public override void Undo(IFileSystem fileSystem) => fileSystem.DeleteFile(Path);
}
