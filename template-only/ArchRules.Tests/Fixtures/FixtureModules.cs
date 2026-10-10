using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Inputs;

namespace Tsikhanau.Monolith.ArchRules.Tests.Fixtures;

internal static class FixtureModules
{
    public const String RootNamespace = "Acme";

    public static readonly ArchModule Good = Create("Good");

    public static readonly ArchModule Peer = Create("Peer");

    public static readonly ArchModule Bad = Create("Bad");

    public static readonly ArchModules Clean = new([Good, Peer], [Kernel]);

    public static readonly ArchModules Dirty = new([Good, Peer, Bad], [Kernel]);

    public static Assembly Kernel => FixtureAssemblies.Get($"{RootNamespace}.SharedKernel");

    public static Assembly Host => FixtureAssemblies.Get($"{RootNamespace}.Bootstrapper");

    private static ArchModule Create(String name)
    {
        var rootNamespace = $"{RootNamespace}.Modules.{name}";

        return new ArchModule(
            name,
            rootNamespace,
            () => [FixtureAssemblies.Get(rootNamespace), FixtureAssemblies.Get($"{rootNamespace}.Contracts")]);
    }
}
