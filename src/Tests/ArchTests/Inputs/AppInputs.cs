using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.Testing.Modules;

namespace ModularMonolith.ArchTests.Inputs;

public sealed class AppInputs : IArchRuleInputs
{
    public static IReadOnlyList<ArchModule> Modules => [.. ProductionModules.Names.Select(CreateModule)];

    public static EmptySubject EmptySubject => EmptySubject.Pass;

    private static ArchModule CreateModule(String name)
    {
        var rootNamespace = $"ModularMonolith.Modules.{name}";

        return new ArchModule(
            name,
            rootNamespace,
            [Assembly.Load(rootNamespace), Assembly.Load($"{rootNamespace}.Contracts")]);
    }
}
