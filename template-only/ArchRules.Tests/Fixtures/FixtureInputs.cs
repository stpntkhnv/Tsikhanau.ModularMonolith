using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Inputs;

namespace Tsikhanau.Monolith.ArchRules.Tests.Fixtures;

public sealed class FixtureInputs : IArchRuleInputs
{
    public static IReadOnlyList<ArchModule> Modules => [FixtureModules.Good, FixtureModules.Peer];

    public static String RootNamespace => FixtureModules.RootNamespace;

    public static EmptySubject EmptySubject => EmptySubject.Pass;

    public static IReadOnlyList<Assembly> SharedKernelAssemblies => [FixtureModules.Kernel];
}
