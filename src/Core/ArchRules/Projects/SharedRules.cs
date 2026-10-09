using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.Shared.Reflection;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Architecture = ArchUnitNET.Domain.Architecture;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public static class SharedRules
{
    private const String SharedNamespace = "Tsikhanau.Monolith.Shared";
    private const String CoreNamespacesOtherThanShared = "^Tsikhanau\\.Monolith(\\.(?!Shared(\\.|$)).+)?$";

    public static RuleEvaluation SharedCodeDoesNotDependOnTheRestOfTheApp(ArchSolution solution)
    {
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(NamespacePatterns.Build(SharedNamespace))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(CoreNamespacesOtherThanShared)
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(NamespacePatterns.Build(solution.RootNamespace));

        IReadOnlyList<Type> sharedTypes = [.. CoreAssemblies.Shared.HandwrittenTypes.Where(IsShared)];

        return RuleEvaluation.Create(sharedTypes, () => rule.GetViolationsIn(LoadArchitecture()));
    }

    private static Boolean IsShared(Type type) => type.IsInNamespace(SharedNamespace);

    private static Architecture LoadArchitecture() =>
        CoreAssemblies.BuildingBlocks
            .Prepend(CoreAssemblies.Shared)
            .LoadArchitecture();
}
