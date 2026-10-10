using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Modules;

public sealed class ModuleGraphTests
{
    private static readonly ModuleGraph Graph = new(
        new Dictionary<String, IReadOnlyList<String>>
        {
            ["Billing"] = ["Tags", "Zones"],
            ["Tags"] = ["Notes"],
            ["Notes"] = [],
            ["Zones"] = ["Zones"],
        });

    [Fact]
    public void Path_through_the_references_of_the_modules_is_found_with_every_module_on_it()
    {
        var path = Graph.FindPath("Billing", "Notes");

        path
            .Map(modules => String.Join(" -> ", modules))
            .ShouldBe(Optional<String>.Some("Billing -> Tags -> Notes"));
    }

    [Theory]
    [InlineData("Notes", "Billing")]
    [InlineData("Zones", "Notes")]
    [InlineData("Orders", "Notes")]
    public void Module_that_does_not_reach_another_has_no_path_to_it(String from, String to)
    {
        var path = Graph.FindPath(from, to);

        path.HasValue.ShouldBeFalse();
    }

    [Theory]
    [InlineData(@"..\..\Notes\Acme.Shop.Modules.Notes.Contracts\Acme.Shop.Modules.Notes.Contracts.csproj", "Notes")]
    [InlineData("../../Notes/Acme.Shop.Modules.Notes.Contracts/Acme.Shop.Modules.Notes.Contracts.csproj", "Notes")]
    [InlineData(@"..\Acme.Shop.Modules.Tags.Contracts\Acme.Shop.Modules.Tags.Contracts.csproj", "Tags")]
    [InlineData(@"..\..\..\Core\Contracts\Tsikhanau.Monolith.Contracts.csproj", "")]
    [InlineData(@"..\..\Shared\Acme.Shop.Shared.Contracts\Acme.Shop.Shared.Contracts.csproj", "")]
    [InlineData(@"..\..\Notes\Contoso.Hub.Modules.Notes.Contracts\Contoso.Hub.Modules.Notes.Contracts.csproj", "")]
    [InlineData(@"..\..\Notes\Acme.Shop.Modules.Notes\Acme.Shop.Modules.Notes.csproj", "")]
    [InlineData(@"..\Acme.Shop.Modules.Contracts\Acme.Shop.Modules.Contracts.csproj", "")]
    public void Module_is_read_from_the_contracts_project_a_reference_includes(String include, String module)
    {
        var referencedModule = ModuleGraph.FindModuleOfContracts(include, "Acme.Shop");

        referencedModule.ShouldBe(module.Length == 0 ? Optional<String>.None() : module);
    }

    [Theory]
    [InlineData("Tags", "Notes", true)]
    [InlineData("Billing", "Notes", false)]
    [InlineData("Notes", "Tags", false)]
    [InlineData("Orders", "Notes", false)]
    public void Module_references_only_the_contracts_its_project_names(String from, String to, Boolean references)
    {
        var referencesContracts = Graph.References(from, to);

        referencesContracts.ShouldBe(references);
    }
}
