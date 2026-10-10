using Tsikhanau.Monolith.Scaffold.Features;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Features;

public sealed class FeatureRequestTests
{
    private const String PascalCaseRule = "must be PascalCase: an ASCII capital letter, then letters and digits";

    private const String CommandAnsweringGet =
        "a command answers POST, PUT or DELETE, not GET: a GET only reads, "
        + "so give it a query (--kind query --endpoint get)";

    private const String ChangeIsCommand =
        "a change of state is a command (--kind command --endpoint post, put or delete)";

    private const String PublicQueryWithEndpoint =
        "a public query has no endpoint: neighbours ask INotesQueryFacade for it; "
        + "for HTTP add a query of the module (--kind query --endpoint get)";

    [Theory]
    [InlineData("ArchiveNote", "None")]
    [InlineData("CreateNote", "Post")]
    [InlineData("RenameNote", "Put")]
    [InlineData("DeleteNote", "Delete")]
    [InlineData("Import2Files", "None")]
    [InlineData("PurgeHTTPLogs", "None")]
    [InlineData("Commander", "None")]
    public void Command_with_a_pascal_case_name_and_a_method_that_changes_state_is_valid(String name, String method)
    {
        var request = CreateCommand(name, Enum.Parse<EndpointMethod>(method));

        var validated = request.Validate();

        validated.ShouldBe(Result.Success(request));
    }

    [Theory]
    [InlineData("archiveNote", PascalCaseRule)]
    [InlineData("Archive_Note", PascalCaseRule)]
    [InlineData("Notes.ArchiveNote", PascalCaseRule)]
    [InlineData("ArchiveNöte", PascalCaseRule)]
    [InlineData("ArchiveNoteCommand", "must not end in Command: scaffold adds the suffix of each type itself")]
    [InlineData("ArchiveNoteHandler", "must not end in Handler: scaffold adds the suffix of each type itself")]
    [InlineData("NoteRequest", "must not end in Request: scaffold adds the suffix of each type itself")]
    [InlineData("NoteEndpoint", "must not end in Endpoint: scaffold adds the suffix of each type itself")]
    [InlineData("NoteTests", "must not end in Tests: scaffold adds the suffix of each type itself")]
    [InlineData("GetNoteQuery", "must not end in Query: scaffold adds the suffix of each type itself")]
    [InlineData("NoteDto", "must not end in Dto: scaffold adds the suffix of each type itself")]
    [InlineData("NoteCreatedEvent", "must not end in Event: scaffold adds the suffix of each type itself")]
    public void Bad_or_suffixed_name_is_refused_with_the_rule_it_breaks(String name, String rule)
    {
        var request = CreateCommand(name, EndpointMethod.None);

        var validated = request.Validate();

        var message = validated.Match(_ => "valid", error => error.Message);

        message.ShouldBe($"feature name '{name}' {rule}");
    }

    [Theory]
    [InlineData("CountNotes", "Query", "None")]
    [InlineData("GetNoteSummary", "Query", "Get")]
    [InlineData("GetTag", "PublicQuery", "None")]
    public void Query_answers_get_or_nothing_and_a_public_query_answers_nothing(
        String name,
        String kind,
        String method)
    {
        var request = new FeatureRequest(
            "Notes",
            name,
            Enum.Parse<FeatureKind>(kind),
            Enum.Parse<EndpointMethod>(method));

        var validated = request.Validate();

        validated.ShouldBe(Result.Success(request));
    }

    [Theory]
    [InlineData("Command", "Get", CommandAnsweringGet)]
    [InlineData("Query", "Post", $"a query answers only GET, not POST: {ChangeIsCommand}")]
    [InlineData("Query", "Put", $"a query answers only GET, not PUT: {ChangeIsCommand}")]
    [InlineData("Query", "Delete", $"a query answers only GET, not DELETE: {ChangeIsCommand}")]
    [InlineData("PublicQuery", "Get", PublicQueryWithEndpoint)]
    [InlineData("PublicQuery", "Post", PublicQueryWithEndpoint)]
    public void Endpoint_method_that_does_not_fit_the_kind_is_refused(String kind, String method, String problem)
    {
        var request = new FeatureRequest(
            "Notes",
            "ShowNote",
            Enum.Parse<FeatureKind>(kind),
            Enum.Parse<EndpointMethod>(method));

        var validated = request.Validate();

        var message = validated.Match(_ => "valid", error => error.Message);

        message.ShouldBe(problem);
    }

    private static FeatureRequest CreateCommand(String name, EndpointMethod method) =>
        new("Notes", name, FeatureKind.Command, method);
}
