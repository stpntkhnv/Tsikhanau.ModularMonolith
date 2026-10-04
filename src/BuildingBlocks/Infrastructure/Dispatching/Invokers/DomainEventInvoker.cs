using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.BuildingBlocks.Domain.Events;
using ModularMonolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Dispatching.Invokers;

internal abstract class DomainEventInvoker
{
    private static readonly ConcurrentDictionary<Type, DomainEventInvoker> Cache = new();

    public static DomainEventInvoker Get(Type eventType) =>
        Cache.GetOrAdd(eventType, static type =>
            typeof(DomainEventInvoker<>)
                .MakeGenericType(type)
                .CreateInstance<DomainEventInvoker>());

    public abstract Task<Result<Unit>> InvokeAsync(
        DomainEvent domainEvent,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class DomainEventInvoker<TEvent> : DomainEventInvoker
    where TEvent : DomainEvent
{
    public override async Task<Result<Unit>> InvokeAsync(
        DomainEvent domainEvent,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
        {
            var result = await handler.Handle((TEvent)domainEvent, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }
}
