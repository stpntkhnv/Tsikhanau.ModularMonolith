using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Outbox;

public sealed class OutboxPump<TModule>(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<QueueSettings> options,
    IEventBus eventBus,
    IntegrationEventRegistry integrationEventRegistry,
    SubscriptionRegistry subscriptionRegistry,
    QueueSignals queueSignals,
    TimeProvider timeProvider,
    ILogger<OutboxPump<TModule>> logger)
    : MessagePump<TModule, OutboxMessage>(serviceScopeFactory, options, timeProvider, logger)
    where TModule : IModuleFacade
{
    protected override String Table => OutboxMessage.Table;

    protected override String OrderColumn => "occurred_on";

    protected override Task<Result<Unit>> HandleAsync(
        IServiceProvider serviceProvider,
        ModuleDbContext dbContext,
        OutboxMessage message,
        DateTimeOffset processedOn,
        CancellationToken cancellationToken) =>
        integrationEventRegistry
            .FindType(message.Type)
            .ToResult(() => MessagingErrors.UnknownType(message.Type))
            .BindAsync(_ => DeliverAsync(dbContext, message, processedOn, cancellationToken));

    protected override Boolean IsSecretType(String type) => integrationEventRegistry.ContainsSecrets(type);

    private async Task<Result<Unit>> DeliverAsync(
        ModuleDbContext dbContext,
        OutboxMessage message,
        DateTimeOffset processedOn,
        CancellationToken cancellationToken)
    {
        await eventBus.DeliverAsync(message, dbContext, cancellationToken);
        EraseSecretsOf(message);
        message.ProcessedOn = processedOn;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    protected override void OnCommitted(OutboxMessage message) =>
        subscriptionRegistry
            .GetSubscribers(message.Type)
            .ForEach(subscriber => queueSignals.Signal(subscriber.Key));
}
