using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Dispatching.Invokers;

internal abstract class IntegrationEventInvoker
{
    private static readonly ConcurrentDictionary<Type, IntegrationEventInvoker> Cache = new();

    public static IntegrationEventInvoker Get(Type eventType) =>
        Cache.GetOrAdd(eventType, static type =>
            typeof(IntegrationEventInvoker<>)
                .MakeGenericType(type)
                .CreateInstance<IntegrationEventInvoker>());

    public abstract Task<Result<Unit>> InvokeAsync(
        IntegrationEvent integrationEvent,
        Object moduleKey,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class IntegrationEventInvoker<TEvent> : IntegrationEventInvoker
    where TEvent : IntegrationEvent
{
    public override async Task<Result<Unit>> InvokeAsync(
        IntegrationEvent integrationEvent,
        Object moduleKey,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        foreach (var handler in serviceProvider.GetKeyedServices<IIntegrationEventHandler<TEvent>>(moduleKey))
        {
            var result = await handler.Handle((TEvent)integrationEvent, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }
}
