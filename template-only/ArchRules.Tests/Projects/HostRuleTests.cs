using Tsikhanau.Monolith.ArchRules.Projects.Hosts;
using Tsikhanau.Monolith.ArchRules.Tests.Fixtures;
using static Tsikhanau.Monolith.ArchRules.Tests.Fixtures.FixtureModules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Projects;

public sealed class HostRuleTests
{
    private const String GoodAddedTwice = "Acme.RepeatingBootstrapper adds Acme.Modules.Good.GoodModule more than once";

    [Fact]
    public void Host_rule_names_a_module_that_the_host_adds_twice()
    {
        var evaluation = HostRules.HostAddsEveryModuleFolder(RepeatingHost, Clean);

        evaluation.ShouldFindExactly([GoodAddedTwice]);
    }
}
