using System.Collections.Concurrent;
using System.Collections.Immutable;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;

namespace ModularMonolith.BuildingBlocks.Infrastructure.EventBus;

public sealed class SubscriptionRegistry
{
    private readonly ConcurrentDictionary<String, ImmutableArray<ModuleDescriptor>> _subscribers =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<(String EventName, Type ModuleKey), Boolean> _runsOutsideTransaction = new();

    public void Subscribe(String eventName, ModuleDescriptor module, Boolean runsOutsideTransaction)
    {
        _subscribers.AddOrUpdate(
            eventName,
            _ => [module],
            (_, existingSubscribers) =>
                existingSubscribers.Contains(module) ? existingSubscribers : existingSubscribers.Add(module));

        _runsOutsideTransaction[(eventName, module.Key)] = runsOutsideTransaction;
    }

    public ImmutableArray<ModuleDescriptor> GetSubscribers(String eventName) =>
        _subscribers.GetValueOrDefault(eventName, []);

    public Boolean RunsOutsideTransaction(String eventName, Type moduleKey) =>
        _runsOutsideTransaction.GetValueOrDefault((eventName, moduleKey));
}
