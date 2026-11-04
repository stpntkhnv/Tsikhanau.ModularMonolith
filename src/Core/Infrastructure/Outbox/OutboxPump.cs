using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.Infrastructure.Queues.Messages;
using Tsikhanau.Monolith.Infrastructure.Queues.Metrics;
using Tsikhanau.Monolith.Infrastructure.Queues.Processing;
using Tsikhanau.Monolith.Infrastructure.Queues.Pumps;
using Tsikhanau.Monolith.Infrastructure.Queues.Secrets;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Outbox;

public sealed class OutboxPump<TModule>(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<QueueSettings> options,
    QueueMetrics queueMetrics,
    IEventBus eventBus,
    IntegrationEventRegistry integrationEventRegistry,
    SubscriptionRegistry subscriptionRegistry,
    QueueSignals queueSignals,
    TimeProvider timeProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<OutboxPump<TModule>> logger)
    : MessagePump<TModule, OutboxMessage>(
        serviceScopeFactory,
        options,
        queueMetrics,
        timeProvider,
        hostApplicationLifetime,
        logger)
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
        FindKnownType(message)
            .BindAsync(_ => DeliverAndMarkProcessedAsync(dbContext, message, processedOn, cancellationToken));

    protected override Boolean IsSecretType(String type) => integrationEventRegistry.ContainsSecrets(type);

    private Result<Type> FindKnownType(OutboxMessage message)
    {
        var knownType = integrationEventRegistry.FindType(message.Type);
        return knownType.HasValue
            ? knownType.Value
            : MessagingErrors.UnknownType(message.Type);
    }

    private async Task<Result<Unit>> DeliverAndMarkProcessedAsync(
        ModuleDbContext dbContext,
        OutboxMessage message,
        DateTimeOffset processedOn,
        CancellationToken cancellationToken)
    {
        await eventBus.DeliverAsync(message, IsSecret(message), dbContext, cancellationToken);
        MarkProcessed(message, processedOn);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    protected override void OnCommitted(OutboxMessage message) =>
        subscriptionRegistry
            .GetSubscribers(message.Type)
            .ForEach(subscriber => queueSignals.Signal(subscriber.Key));
}
