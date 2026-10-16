using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.InternalCommands;

public sealed class InternalCommandsPump<TModule>(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<QueueSettings> options,
    InternalCommandRegistry<TModule> internalCommandRegistry,
    TimeProvider timeProvider,
    ILogger<InternalCommandsPump<TModule>> logger)
    : MessagePump<TModule, InternalCommandMessage>(serviceScopeFactory, options, timeProvider, logger)
    where TModule : IModuleFacade
{
    protected override String Table => InternalCommandMessage.Table;

    protected override String OrderColumn => "enqueued_on";

    protected override Task<Result<Unit>> HandleAsync(
        IServiceProvider serviceProvider,
        ModuleDbContext dbContext,
        InternalCommandMessage message,
        DateTimeOffset processedOn,
        CancellationToken cancellationToken) =>
        internalCommandRegistry
            .FindType(message.Type)
            .ToResult(() => MessagingErrors.UnknownType(message.Type))
            .Bind(type => MessageSerializer.Deserialize<InternalCommand>(message.Payload, type))
            .Tap(_ => message.ProcessedOn = processedOn)
            .Tap(_ => EraseSecretsOf(message))
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
