using Tsikhanau.Monolith.Scaffold.Plans;

namespace Tsikhanau.Monolith.Scaffold.Facades;

internal sealed record FacadeChange(IReadOnlyList<NewFile> NewFiles, IReadOnlyList<FileEdit> FileEdits)
{
    public static readonly FacadeChange None = new([], []);
}
