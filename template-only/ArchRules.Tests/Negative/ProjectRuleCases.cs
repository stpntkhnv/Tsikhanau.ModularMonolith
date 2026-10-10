using Tsikhanau.Monolith.ArchRules.Projects;
using static Tsikhanau.Monolith.ArchRules.Tests.Fixtures.FixtureModules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Negative;

internal static class ProjectRuleCases
{
    public static IEnumerable<RuleCase> All =>
    [
        new(
            typeof(HostRules),
            nameof(HostRules.HostAddsEveryModuleFolder),
            () => HostRules.HostAddsEveryModuleFolder(Host, Dirty),
            ["Acme.Bootstrapper does not add src/Modules/Bad (Acme.Modules.Bad.BadModule)"],
            () => HostRules.HostAddsEveryModuleFolder(Host, Clean)),
    ];
}
