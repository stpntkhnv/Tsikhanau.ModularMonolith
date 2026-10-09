using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.TestModules.Pings;

namespace Tsikhanau.Monolith.ArchRules.Tests.Inputs;

public sealed class TestModuleInputs : IArchRuleInputs
{
    public static IReadOnlyList<ArchModule> Modules => [CreateModule("Pings"), CreateModule("Pongs")];

    public static EmptySubject EmptySubject => EmptySubject.Skip;

    private static ArchModule CreateModule(String name) =>
        new(name, $"Tsikhanau.Monolith.TestModules.{name}", [typeof(PingsModule).Assembly]);
}
