using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class IntegrationEventNameRules
{
    public static RuleEvaluation EveryIntegrationEventHasAUniqueStableName(ArchModules modules)
    {
        IReadOnlyList<Type> eventTypes = [.. modules.Types.Implementing<IntegrationEvent>()];
        return RuleEvaluation.Create(eventTypes, () => [.. GetUnnamed(eventTypes), .. GetSharedNames(eventTypes)]);
    }

    private static IEnumerable<String> GetUnnamed(IEnumerable<Type> eventTypes) =>
        eventTypes
            .Where(type => FindEventName(type).IsNone)
            .Select(type => $"{type.FullName} has no [IntegrationEventName]");

    private static IEnumerable<String> GetSharedNames(IEnumerable<Type> eventTypes) =>
        eventTypes
            .Select(FindEventName)
            .Where(name => name.HasValue)
            .GroupBy(name => name.Value)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} names more than one integration event");

    private static Optional<String> FindEventName(Type type) =>
        Optional<String>.FromNullable(type.GetCustomAttribute<IntegrationEventNameAttribute>()?.Name);
}
