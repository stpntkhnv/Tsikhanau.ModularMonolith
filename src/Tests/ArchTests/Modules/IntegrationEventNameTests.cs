using System.Reflection;
using ModularMonolith.ArchTests.Support;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace ModularMonolith.ArchTests.Modules;

public sealed class IntegrationEventNameTests
{
    [Fact]
    public void Every_integration_event_has_a_unique_stable_name()
    {
        var eventTypes = ArchModules.AllModuleTypes
            .Implementing<IntegrationEvent>()
            .ToList();

        var unnamedEventTypeNames = eventTypes
            .Where(type => FindEventName(type).IsNone)
            .Select(type => type.FullName);

        var sharedEventNames = eventTypes
            .GroupBy(FindEventName)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        unnamedEventTypeNames.ShouldBeEmpty();
        sharedEventNames.ShouldBeEmpty();
    }

    private static Optional<String> FindEventName(Type type) =>
        Optional<String>.FromNullable(type.GetCustomAttribute<IntegrationEventNameAttribute>()?.Name);
}
