using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Domain.Events;
using Tsikhanau.Monolith.Infrastructure.Dispatching.Invokers;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Railway;
using Xunit;

namespace Tsikhanau.Monolith.Testing.Queues;

internal static class UnitOfWorkDriver
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<Result<Unit>> DispatchAsync<TModule>(
        IServiceProvider serviceProvider,
        DomainEvent domainEvent)
        where TModule : IModuleFacade
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        EnsureHandled<TModule>(scope.ServiceProvider, domainEvent.GetType());
        return await DomainEventInvoker
            .Get(domainEvent.GetType())
            .InvokeAsync(domainEvent, scope.ServiceProvider, CancellationToken)
            .BindAsync(_ => CommitAsync<TModule>(scope.ServiceProvider));
    }

    public static async Task EnqueueAsync<TModule>(IServiceProvider serviceProvider, InternalCommand internalCommand)
        where TModule : IModuleFacade
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        scope.ServiceProvider
            .GetRequiredService<IInternalCommandScheduler<TModule>>()
            .Enqueue(internalCommand);

        var committed = await CommitAsync<TModule>(scope.ServiceProvider);
        if (committed.IsFailure)
        {
            throw new InvalidOperationException(
                $"Enqueuing {internalCommand.GetType().Name} failed: {committed.Error}");
        }
    }

    private static void EnsureHandled<TModule>(IServiceProvider serviceProvider, Type eventType)
        where TModule : IModuleFacade
    {
        if (!IsHandledIn<TModule>(serviceProvider, eventType))
        {
            throw new InvalidOperationException(
                $"No handler in {typeof(TModule).Name} handles {eventType.Name} ({eventType.FullName}); "
                + $"a module handles a domain event with an IDomainEventHandler<{eventType.Name}> "
                + "in its own namespace");
        }
    }

    private static Boolean IsHandledIn<TModule>(IServiceProvider serviceProvider, Type eventType)
        where TModule : IModuleFacade =>
        serviceProvider
            .GetServices(typeof(IDomainEventHandler<>).MakeGenericType(eventType))
            .OfType<Object>()
            .Any(handler => IsInModule<TModule>(handler.GetType()));

    private static Boolean IsInModule<TModule>(Type handlerType)
        where TModule : IModuleFacade =>
        typeof(TModule).Namespace is { } moduleNamespace && handlerType.IsInNamespace(moduleNamespace);

    private static Task<Result<Unit>> CommitAsync<TModule>(IServiceProvider serviceProvider)
        where TModule : IModuleFacade =>
        serviceProvider
            .GetRequiredService<IUnitOfWork<TModule>>()
            .CommitAsync(CancellationToken);
}
