using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.Testing.Modules;

namespace ModularMonolith.ArchTests.Inputs;

public sealed class AppInputs : IAppArchRuleInputs
{
    public static IReadOnlyList<ArchModule> Modules => [.. ProductionModules.Names.Select(CreateModule)];

    public static String RootNamespace => "ModularMonolith";

    public static String SolutionRoot => SolutionPaths.Root;

    public static Assembly HostAssembly => typeof(Program).Assembly;

    public static EmptySubject EmptySubject => EmptySubject.Pass;

    private static ArchModule CreateModule(String name)
    {
        var moduleNamespace = $"{RootNamespace}.Modules.{name}";

        return new ArchModule(
            name,
            moduleNamespace,
            [Assembly.Load(moduleNamespace), Assembly.Load($"{moduleNamespace}.Contracts")]);
    }
}
