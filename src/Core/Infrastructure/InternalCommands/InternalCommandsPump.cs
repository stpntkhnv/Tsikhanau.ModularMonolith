using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.Infrastructure.Queues.Messages;
using Tsikhanau.Monolith.Infrastructure.Queues.Metrics;
using Tsikhanau.Monolith.Infrastructure.Queues.Pumps;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.InternalCommands;

public sealed class InternalCommandsPump<TModule>(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<QueueSettings> options,
    QueueMetrics queueMetrics,
    InternalCommandRegistry<TModule> internalCommandRegistry,
    TimeProvider timeProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<InternalCommandsPump<TModule>> logger)
    : MessagePump<TModule, InternalCommandMessage>(
        serviceScopeFactory,
        options,
        queueMetrics,
        timeProvider,
        hostApplicationLifetime,
        logger)
    where TModule : IModuleFacade
{
    protected override String Table => InternalCommandMessage.Table;

    protected override String OrderColumn => InternalCommandMessage.OrderColumn;

    protected override Task<Result<Unit>> HandleAsync(
        IServiceProvider serviceProvider,
        ModuleDbContext dbContext,
        InternalCommandMessage message,
        DateTimeOffset processedOn,
        CancellationToken cancellationToken) =>
        internalCommandRegistry
            .GetKnownType(message.Type)
            .Bind(type => MessageSerializer.Deserialize<InternalCommand>(message.Payload, type))
            .Tap(_ => MarkProcessed(message, processedOn))
            .BindAsync(command => SendAsync(command, serviceProvider, cancellationToken));

    protected override Boolean IsSecretType(String type) => internalCommandRegistry.ContainsSecrets(type);

    private static Task<Result<Unit>> SendAsync(
        InternalCommand command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        serviceProvider
            .GetRequiredService<ModuleDispatcher<TModule>>()
            .SendAsync(command, cancellationToken);
}
