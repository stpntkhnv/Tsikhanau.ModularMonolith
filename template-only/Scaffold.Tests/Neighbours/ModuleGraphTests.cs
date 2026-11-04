using Tsikhanau.Monolith.Scaffold.Neighbours;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Neighbours;

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
