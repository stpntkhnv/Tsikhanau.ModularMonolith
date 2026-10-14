using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Mutations;

internal sealed class AggregateMutations(SourceTree sourceTree)
{
    private const String NoteTitle = $"{ScratchPaths.Notes}/Domain/Notes/NoteTitle.cs";

    private const String TitleProperty = "    public NoteTitle Title { get; private set; }";

    private const String RaisedEvent = "note.Raise(new NoteCreatedDomainEvent(createdOn, id, title));";

    public Result<Unit> PublicSetterOnAggregate() =>
        sourceTree.Replace(
            ScratchPaths.Note,
            "public NoteTitle Title { get; private set; }",
            "public NoteTitle Title { get; set; }");

    public Result<Unit> PublicFieldAndStaticCounterOnAggregate() =>
        sourceTree
            .Replace(
                ScratchPaths.Note,
                TitleProperty,
                $"    public Int32 Revision;\n\n    public static Int32 CreatedCount;\n\n{TitleProperty}")
            .Bind(_ => sourceTree.Replace(
                ScratchPaths.Note,
                "        var note = new Note(id) { Title = title };",
                "        var note = new Note(id) { Title = title, Revision = 1 };\n        CreatedCount++;"));

    public Result<Unit> PublicConstructorOnAggregate() =>
        sourceTree.Replace(
            ScratchPaths.Note,
            "    private Note(NoteId id) : base(id) { }",
            "    public Note(NoteId id) : base(id) { }");

    public Result<Unit> StateChangedWithoutEvent() =>
        sourceTree.Replace(
            ScratchPaths.Note,
            "    public static Note Create(",
            "    public void Rename(NoteTitle title) => Title = title;\n\n    public static Note Create(");

    public Result<Unit> DomainReadsTheClock() =>
        sourceTree.Replace(
            ScratchPaths.Note,
            RaisedEvent,
            "note.Raise(new NoteCreatedDomainEvent(DateTimeOffset.UtcNow, id, title));");

    public Result<Unit> DomainUsesSystemTimeProvider() =>
        sourceTree.Replace(
            ScratchPaths.Note,
            RaisedEvent,
            "note.Raise(new NoteCreatedDomainEvent(TimeProvider.System.GetUtcNow(), id, title));");

    public Result<Unit> KeywordTypeNames() =>
        sourceTree
            .Replace(NoteTitle, "public const Int32 MaxLength = 50;", "public const int MaxLength = 50;")
            .Bind(_ => sourceTree.Replace(
                NoteTitle,
                "public static Result<NoteTitle> Create(String value, String fieldName)",
                "public static Result<NoteTitle> Create(string value, string fieldName)"));
}
