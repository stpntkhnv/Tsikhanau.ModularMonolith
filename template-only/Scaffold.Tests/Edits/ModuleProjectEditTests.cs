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

    private const String Notes2Contracts =
        @"    <ProjectReference Include=""..\..\Notes2\Acme.Shop.Modules.Notes2.Contracts\"
        + @"Acme.Shop.Modules.Notes2.Contracts.csproj"" />";

    private const String OwnContractsWithForwardSlashes =
        """    <ProjectReference Include="../Acme.Shop.Modules.Tags.Contracts/"""
        + """Acme.Shop.Modules.Tags.Contracts.csproj" />""";

    private const String OwnContractsWithChildren =
        @"    <ProjectReference Include=""..\Acme.Shop.Modules.Tags.Contracts\"
        + @"Acme.Shop.Modules.Tags.Contracts.csproj"">"
        + "\n      <Private>true</Private>\n    </ProjectReference>";

    private const String BillingContractsWithChildren =
        @"    <ProjectReference Include=""..\..\Billing\Acme.Shop.Modules.Billing.Contracts\"
        + @"Acme.Shop.Modules.Billing.Contracts.csproj"">"
        + "\n      <Private>true</Private>\n    </ProjectReference>";

    private const String NovaContracts =
        @"    <ProjectReference Include=""..\..\NOVA\Acme.Shop.Modules.NOVA.Contracts\"
        + @"Acme.Shop.Modules.NOVA.Contracts.csproj"" />";

    private const String CoreDomain =
        @"    <ProjectReference Include=""..\..\..\Core\Domain\Tsikhanau.Monolith.Domain.csproj"" />";

    private const String CoreDomainWithChildren =
        @"    <ProjectReference Include=""..\..\..\Core\Domain\Tsikhanau.Monolith.Domain.csproj"">"
        + "\n      <Private>true</Private>\n    </ProjectReference>";

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
    [InlineData(new[] { Notes2Contracts }, new[] { NotesContracts, Notes2Contracts })]
    [InlineData(new[] { NovaContracts }, new[] { NotesContracts, NovaContracts })]
    [InlineData(new[] { BillingContractsWithChildren }, new[] { BillingContractsWithChildren, NotesContracts })]
    public void Neighbour_contracts_are_sorted_among_the_other_neighbours(String[] neighbours, String[] sorted)
    {
        var project = WithReferencesAfterOwnContracts(neighbours);

        var edit = AddNotesContracts(project);

        ReadText(edit)
            .ShouldBe(WithReferencesAfterOwnContracts(sorted));
    }

    [Fact]
    public void Element_with_children_after_the_contracts_of_the_module_stays_whole()
    {
        var project = TagsProject.Replace(CoreDomain, CoreDomainWithChildren, StringComparison.Ordinal);

        var edit = AddNotesContracts(project);

        ReadText(edit)
            .ShouldBe(project.Replace(OwnContracts, $"{OwnContracts}\n{NotesContracts}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(OwnContractsWithForwardSlashes)]
    [InlineData(OwnContractsWithChildren)]
    public void Contracts_of_the_module_written_another_way_are_the_anchor_too(String ownContracts)
    {
        var project = TagsProject.Replace(OwnContracts, ownContracts, StringComparison.Ordinal);

        var edit = AddNotesContracts(project);

        ReadText(edit)
            .ShouldBe(project.Replace(ownContracts, $"{ownContracts}\n{NotesContracts}", StringComparison.Ordinal));
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
