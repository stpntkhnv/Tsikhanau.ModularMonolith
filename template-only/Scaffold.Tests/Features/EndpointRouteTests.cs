using Tsikhanau.Monolith.Scaffold.Features;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Features;

public sealed class EndpointRouteTests
{
    private const String SegmentRule = "a segment is kebab-case words (archive, note-tags) or an id {name:guid}";

    private const String GuidIdRule = "an id is a Guid v7, so its parameter is {name:guid}";

    [Theory]
    [InlineData("")]
    [InlineData("/archive")]
    [InlineData("/archived-notes")]
    [InlineData("/{noteId:guid}")]
    [InlineData("/{noteId:guid}/tags")]
    [InlineData("/{noteId:guid}/tags/{tagId:guid}")]
    [InlineData("/v2/import2-files")]
    public void Empty_route_or_kebab_case_words_and_guid_ids_after_slashes_is_valid(String path)
    {
        var parsedPath = EndpointRoute
            .Parse(path)
            .Map(route => route.Path);

        parsedPath.ShouldBe(Result.Success(path));
    }

    [Theory]
    [InlineData("notes", "must be empty, the collection of the module, or start with /")]
    [InlineData("{noteId:guid}", "must be empty, the collection of the module, or start with /")]
    [InlineData("/", "must not end in /")]
    [InlineData("/notes/", "must not end in /")]
    [InlineData("/notes//tags", "has an empty segment between two /")]
    [InlineData("/Bad_Segment", $"has the segment 'Bad_Segment': {SegmentRule}")]
    [InlineData("/archived-", $"has the segment 'archived-': {SegmentRule}")]
    [InlineData("/note tags", $"has the segment 'note tags': {SegmentRule}")]
    [InlineData("/{NoteId:guid}", $"has the segment '{{NoteId:guid}}': {SegmentRule}")]
    [InlineData("/{id}", $"has the parameter {{id}}: {GuidIdRule}")]
    [InlineData("/{id:int}", $"has the parameter {{id:int}}: {GuidIdRule}")]
    [InlineData("/{id:guid?}", $"has the parameter {{id:guid?}}: {GuidIdRule}")]
    [InlineData("/{id:guid}/{id:guid}", "names the parameter id twice: each parameter needs its own name")]
    [InlineData(
        "/{noteId:guid}/tags/{noteid:guid}",
        "names the parameter noteId twice: each parameter needs its own name")]
    public void Route_that_breaks_a_route_rule_is_refused_with_the_rule(String path, String rule)
    {
        var message = EndpointRoute
            .Parse(path)
            .Match(_ => "valid", error => error.Message);

        message.ShouldBe($"route '{path}' {rule}");
    }
}
