using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Transactions;

internal sealed class Journal
{
    private readonly List<JournalEntry> _entries = [];

    public Boolean IsEmpty => _entries.Count == 0;

    public void Record(JournalEntry entry) => _entries.Add(entry);

    public void Clear() => _entries.Clear();

    public Result<Unit> RollBack(IFileSystem fileSystem)
    {
        var problems = Enumerable
            .Reverse(_entries)
            .SelectMany(entry => Undo(entry, fileSystem))
            .ToList();

        _entries.Clear();

        return problems.Count == 0
            ? Unit.Value
            : Error.Failure("scaffold.rollback", String.Join("; ", problems));
    }

    private static IEnumerable<String> Undo(JournalEntry entry, IFileSystem fileSystem) =>
        fileSystem
            .Attempt(() => entry.Undo(fileSystem), entry.UndoFailure)
            .Match(_ => Array.Empty<String>(), error => [error.Message]);
}
