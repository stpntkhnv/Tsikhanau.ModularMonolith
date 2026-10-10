using Tsikhanau.Monolith.Scaffold.Features;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Features;

public sealed class FeatureRequestTests
{
    private const String PascalCaseRule = "must be PascalCase: an ASCII capital letter, then letters and digits";

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

    [Fact]
    public void Command_that_answers_get_is_refused()
    {
        var request = CreateCommand("ShowNote", EndpointMethod.Get);

        var validated = request.Validate();

        var message = validated.Match(_ => "valid", error => error.Message);

        message.ShouldBe("a command answers POST, PUT or DELETE, not GET: a GET only reads");
    }

    private static FeatureRequest CreateCommand(String name, EndpointMethod method) =>
        new("Notes", name, FeatureKind.Command, method);
}
