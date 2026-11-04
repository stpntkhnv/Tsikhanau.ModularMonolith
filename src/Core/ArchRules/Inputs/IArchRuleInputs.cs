using System.Reflection;
using Tsikhanau.Monolith.Testing.Modules;

namespace Tsikhanau.Monolith.ArchRules.Inputs;

public interface IArchRuleInputs
{
    static abstract IReadOnlyList<ArchModule> Modules { get; }

    static abstract String RootNamespace { get; }

    static virtual String SolutionRoot => SolutionPaths.Root;

    static virtual EmptySubject EmptySubject => EmptySubject.Skip;

    static virtual IReadOnlyList<Assembly> SharedKernelAssemblies => [];
}
