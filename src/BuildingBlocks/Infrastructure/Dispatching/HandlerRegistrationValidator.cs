using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.BuildingBlocks.Application.Queries;
using ModularMonolith.BuildingBlocks.Contracts.Queries;
using ModularMonolith.Shared.Reflection;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Dispatching;

internal static class HandlerRegistrationValidator
{
    public static IReadOnlyList<String> CollectProblems(IReadOnlyCollection<Type> types, String rootNamespace) =>
        [
            .. CollectHandlerCountProblems(types),
            .. CollectForeignRequestProblems(types, rootNamespace),
            .. CollectTransactionModeProblems(types),
        ];

    private static IEnumerable<String> CollectHandlerCountProblems(IReadOnlyCollection<Type> types)
    {
        var handledRequests = types
            .Where(type => type.IsConcrete)
            .SelectMany(GetHandledRequests)
            .ToList();

        return types
            .Where(IsRequest)
            .Select(type => (
                Request: type,
                HandlerCount: handledRequests.Count(handledRequest => handledRequest == type)))
            .Where(entry => entry.HandlerCount != 1)
            .Select(entry => $"{entry.Request.FullName} has {entry.HandlerCount} handlers");
    }

    private static IEnumerable<String> CollectForeignRequestProblems(
        IReadOnlyCollection<Type> types,
        String rootNamespace) =>
        types
            .Where(type => type.IsConcrete)
            .SelectMany(handlerType => CollectHandlerForeignRequestProblems(handlerType, rootNamespace));

    private static IEnumerable<String> CollectHandlerForeignRequestProblems(Type handlerType, String rootNamespace) =>
        GetHandledRequests(handlerType)
            .Where(requestType => !requestType.IsInNamespace(rootNamespace))
            .Select(requestType => $"{handlerType.FullName} handles the foreign request {requestType.FullName}");

    private static IEnumerable<String> CollectTransactionModeProblems(IReadOnlyCollection<Type> types) =>
        [.. CollectStrayMarkerProblems(types), .. CollectMixedModeProblems(types)];

    private static IEnumerable<String> CollectStrayMarkerProblems(IReadOnlyCollection<Type> types) =>
        types
            .Where(type => type.IsConcrete && type.Implements<IRunOutsideTransaction>())
            .Where(type => !type.Implements(typeof(IIntegrationEventHandler<>)))
            .Select(type => $"{type.FullName} runs outside a transaction but handles no integration event");

    private static IEnumerable<String> CollectMixedModeProblems(IReadOnlyCollection<Type> types) =>
        types
            .Where(type => type.IsConcrete)
            .SelectMany(GetHandledIntegrationEvents)
            .GroupBy(handled => handled.EventType, handled => handled.RunsOutsideTransaction)
            .Where(HasHandlersInsideAndOutside)
            .Select(group => $"{group.Key.FullName} has handlers both inside and outside a transaction");

    private static Boolean HasHandlersInsideAndOutside(IEnumerable<Boolean> handlersRunOutsideTransaction) =>
        handlersRunOutsideTransaction.Contains(true) && handlersRunOutsideTransaction.Contains(false);

    private static Boolean IsRequest(Type type) =>
        type.IsConcrete && (type.Implements(typeof(ICommand<>)) || type.Implements(typeof(IQuery<>)));

    private static IEnumerable<Type> GetHandledRequests(Type handlerType) =>
        handlerType
            .GetInterfacesBuiltFrom(typeof(ICommandHandler<,>))
            .Concat(handlerType.GetInterfacesBuiltFrom(typeof(IQueryHandler<,>)))
            .Select(contract => contract.GenericTypeArguments[0]);

    private static IEnumerable<(Type EventType, Boolean RunsOutsideTransaction)> GetHandledIntegrationEvents(
        Type handlerType) =>
        handlerType
            .GetInterfacesBuiltFrom(typeof(IIntegrationEventHandler<>))
            .Select(contract => (contract.GenericTypeArguments[0], handlerType.Implements<IRunOutsideTransaction>()));
}
