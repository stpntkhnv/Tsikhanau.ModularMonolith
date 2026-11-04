using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Inputs;

namespace ModularMonolith.ArchTests.Inputs;

public sealed class AppInputs : IAppArchRuleInputs
{
    public static IReadOnlyList<ArchModule> Modules => ArchModule.ForProductionModules(RootNamespace);

    public static String RootNamespace => "ModularMonolith";

    public static Assembly HostAssembly => typeof(Program).Assembly;
}
