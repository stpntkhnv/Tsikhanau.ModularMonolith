namespace Tsikhanau.Monolith.ArchRules.Inputs;

public interface IArchRuleInputs
{
    static abstract IReadOnlyList<ArchModule> Modules { get; }

    static abstract EmptySubject EmptySubject { get; }
}
