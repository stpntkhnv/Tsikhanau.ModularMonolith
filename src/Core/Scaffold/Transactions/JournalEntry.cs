using Tsikhanau.Monolith.Scaffold.Files;

namespace Tsikhanau.Monolith.Scaffold.Transactions;

internal abstract record JournalEntry(String Path)
{
    public abstract String UndoFailure { get; }

    public abstract void Undo(IFileSystem fileSystem);
}
