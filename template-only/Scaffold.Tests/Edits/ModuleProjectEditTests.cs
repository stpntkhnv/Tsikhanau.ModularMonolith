using System.Text;
using Tsikhanau.Monolith.Scaffold.Edits;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Edits;

public sealed class ModuleProjectEditTests
{
    private const String OwnContracts =
        @"    <ProjectReference Include=""..\Acme.Shop.Modules.Tags.Contracts\"
        + @"Acme.Shop.Modules.Tags.Contracts.csproj"" />";

    private const String BillingContracts =
        @"    <ProjectReference Include=""..\..\Billing\Acme.Shop.Modules.Billing.Contracts\"
        + @"Acme.Shop.Modules.Billing.Contracts.csproj"" />";

    private const String NotesContracts =
        @"    <ProjectReference Include=""..\..\Notes\Acme.Shop.Modules.Notes.Contracts\"
        + @"Acme.Shop.Modules.Notes.Contracts.csproj"" />";

    private const String ZonesContracts =
        @"    <ProjectReference Include=""..\..\Zones\Acme.Shop.Modules.Zones.Contracts\"
        + @"Acme.Shop.Modules.Zones.Contracts.csproj"" />";

    private static readonly ScaffoldedModule Tags = SampleModules.Create("Tags");

    private static readonly String TagsProject = new ModuleTemplate(Tags, hasSettings: false)
        .CreateFiles()
        .Single(newFile => newFile.Path == Tags.ProjectPath)
        .Content;

    [Fact]
    public void First_neighbour_contracts_go_right_after_the_contracts_of_the_module()
    {
        var edit = AddNotesContracts(TagsProject);

        ReadText(edit)
            .ShouldBe(TagsProject.Replace(OwnContracts, $"{OwnContracts}\n{NotesContracts}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(new[] { BillingContracts, ZonesContracts }, new[] { BillingContracts, NotesContracts, ZonesContracts })]
    [InlineData(new[] { BillingContracts }, new[] { BillingContracts, NotesContracts })]
    [InlineData(new[] { ZonesContracts }, new[] { NotesContracts, ZonesContracts })]
    public void Neighbour_contracts_are_sorted_among_the_other_neighbours(String[] neighbours, String[] sorted)
    {
        var project = WithReferencesAfterOwnContracts(neighbours);

        var edit = AddNotesContracts(project);

        ReadText(edit)
            .ShouldBe(WithReferencesAfterOwnContracts(sorted));
    }

    [Fact]
    public void Project_with_windows_line_endings_keeps_them()
    {
        var project = TagsProject.Replace("\n", "\r\n", StringComparison.Ordinal);

        var edit = AddNotesContracts(project);

        ReadText(edit)
            .ShouldBe(project.Replace(OwnContracts, $"{OwnContracts}\r\n{NotesContracts}", StringComparison.Ordinal));
    }

    [Fact]
    public void Project_without_the_reference_to_the_contracts_of_the_module_is_refused()
    {
        var project = TagsProject.Replace($"{OwnContracts}\n", String.Empty, StringComparison.Ordinal);

        var edit = AddNotesContracts(project);

        ReadText(edit)
            .ShouldBe(
                $@"{Tags.ProjectPath} must hold the reference to ..\Acme.Shop.Modules.Tags.Contracts\"
                + "Acme.Shop.Modules.Tags.Contracts.csproj");
    }

    private static String WithReferencesAfterOwnContracts(IEnumerable<String> references) =>
        TagsProject.Replace(
            OwnContracts,
            String.Join('\n', [OwnContracts, .. references]),
            StringComparison.Ordinal);

    private static Result<FileEdit> AddNotesContracts(String project) =>
        ModuleProjectEdit.AddNeighbourContracts(SampleModules.CreateFile(Tags.ProjectPath, project), Tags, "Notes");

    private static String ReadText(Result<FileEdit> edit) =>
        edit.Match(fileEdit => Encoding.UTF8.GetString(fileEdit.Updated), error => error.Message);
}
