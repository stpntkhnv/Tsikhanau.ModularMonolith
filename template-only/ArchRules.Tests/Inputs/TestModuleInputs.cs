using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.Testing.Modules;
using Tsikhanau.Monolith.TestModules.Pings;

namespace Tsikhanau.Monolith.ArchRules.Tests.Inputs;

public sealed class TestModuleInputs : IArchRuleInputs
{
    public static IReadOnlyList<ArchModule> Modules => [CreateModule("Pings"), CreateModule("Pongs")];

    public static String RootNamespace => "Tsikhanau.Monolith.TestModules";

    public static String SolutionRoot => SolutionPaths.Root;

    public static EmptySubject EmptySubject => EmptySubject.Skip;

    private static ArchModule CreateModule(String name) =>
        new(name, $"{RootNamespace}.{name}", () => [typeof(PingsModule).Assembly]);
}
