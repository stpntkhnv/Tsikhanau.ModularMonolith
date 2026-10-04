using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;
using ModularMonolith.BuildingBlocks.Infrastructure.Dispatching.Invokers;
using ModularMonolith.BuildingBlocks.Infrastructure.EventBus;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Inbox;

public sealed class InboxPump<TModule>(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<QueueSettings> options,
    IntegrationEventRegistry integrationEventRegistry,
    SubscriptionRegistry subscriptionRegistry,
    TimeProvider timeProvider,
    ILogger<InboxPump<TModule>> logger)
    : MessagePump<TModule, InboxMessage>(serviceScopeFactory, options, timeProvider, logger)
    where TModule : IModuleFacade
{
    protected override String Table => InboxMessage.Table;

    protected override String OrderColumn => "occurred_on";

    protected override Task<Result<Unit>> HandleAsync(
        IServiceProvider serviceProvider,
        ModuleDbContext dbContext,
        InboxMessage message,
        DateTimeOffset processedOn,
        CancellationToken cancellationToken) =>
        DispatchAsync(serviceProvider, message, cancellationToken)
            .TapAsync(_ => message.ProcessedOn = processedOn)
            .TapAsync(_ => integrationEventRegistry.EraseSecretsOf(message))
            .BindAsync(_ => CommitAsync(serviceProvider, cancellationToken));

    protected override Boolean RunsOutsideTransaction(InboxMessage message) =>
        subscriptionRegistry.RunsOutsideTransaction(message.Type, typeof(TModule));

    protected override Task<Result<Unit>> HandleOutsideTransactionAsync(
        IServiceProvider serviceProvider,
        InboxMessage message,
        CancellationToken cancellationToken) =>
        DispatchAsync(serviceProvider, message, cancellationToken)
            .TapAsync(_ => integrationEventRegistry.EraseSecretsOf(message));

    private Task<Result<Unit>> DispatchAsync(
        IServiceProvider serviceProvider,
        InboxMessage message,
        CancellationToken cancellationToken) =>
        integrationEventRegistry
            .FindType(message.Type)
            .ToResult(() => MessagingErrors.UnknownType(message.Type))
            .Bind(type => MessageSerializer.Deserialize<IntegrationEvent>(message.Payload, type))
            .Ensure(
                integrationEvent => HasMessageId(integrationEvent, message),
                _ => MessagingErrors.ForeignPayload(message.Id))
            .BindAsync(integrationEvent => InvokeAsync(integrationEvent, serviceProvider, cancellationToken));

    private static Boolean HasMessageId(IntegrationEvent integrationEvent, InboxMessage message) =>
        integrationEvent.Id != Guid.Empty && integrationEvent.Id == message.Id;

    private static Task<Result<Unit>> InvokeAsync(
        IntegrationEvent integrationEvent,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        IntegrationEventInvoker
            .Get(integrationEvent.GetType())
            .InvokeAsync(integrationEvent, typeof(TModule), serviceProvider, cancellationToken);

    private static Task<Result<Unit>> CommitAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        serviceProvider
            .GetRequiredService<IUnitOfWork<TModule>>()
            .CommitAsync(cancellationToken);
}
