using System.Collections.Concurrent;
using System.Reflection;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.EventBus;

public sealed class IntegrationEventRegistry
{
    private readonly ConcurrentDictionary<Type, String> _names = new();
    private readonly ConcurrentDictionary<Type, Boolean> _secretTypes = new();
    private readonly ConcurrentDictionary<String, Type> _types = new(StringComparer.Ordinal);

    public String GetName(Type eventType) =>
        _names.GetOrAdd(eventType, static type =>
            type.GetCustomAttribute<IntegrationEventNameAttribute>()?.Name
            ?? throw new InvalidOperationException($"{type.FullName} has no IntegrationEventName attribute"));

    public void Register(Type eventType)
    {
        var name = GetName(eventType);
        var registeredType = _types.GetOrAdd(name, eventType);
        if (registeredType != eventType)
        {
            throw new InvalidOperationException(
                $"Integration event name '{name}' is used by {registeredType.FullName} and {eventType.FullName}");
        }
    }

    public Optional<Type> FindType(String name) => _types.GetValueOrDefault(name);

    public Boolean ContainsSecrets(String name) =>
        FindType(name)
            .Map(ContainsSecrets)
            .GetValueOrDefault(false);

    public Boolean ContainsSecrets(Type eventType) =>
        _secretTypes.GetOrAdd(eventType, static type => type.ContainsSecrets);
}
