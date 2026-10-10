using Tsikhanau.Monolith.Scaffold.Transactions;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed record FacadeChange(IReadOnlyList<NewFile> NewFiles, IReadOnlyList<FileEdit> FileEdits)
{
    public static readonly FacadeChange None = new([], []);
}
