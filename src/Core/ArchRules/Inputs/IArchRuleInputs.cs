using System.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Inputs;

public interface IArchRuleInputs
{
    static abstract IReadOnlyList<ArchModule> Modules { get; }

    static abstract String RootNamespace { get; }

    static abstract String SolutionRoot { get; }

    static virtual EmptySubject EmptySubject => EmptySubject.Skip;

    static virtual IReadOnlyList<Assembly> SharedKernelAssemblies => [];
}
