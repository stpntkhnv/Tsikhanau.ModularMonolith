using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.SendSecret;

internal sealed class SendSecretCommandHandler(IOutbox<IPingsModule> outbox, TimeProvider timeProvider)
    : ICommandHandler<SendSecretCommand, Unit>
{
    public Task<Result<Unit>> Handle(SendSecretCommand command, CancellationToken cancellationToken)
    {
        outbox.Add(new PingSecretSentIntegrationEvent(timeProvider.GetUtcNow(), command.PingId, command.Secret));
        return Task.FromResult(Result.Success());
    }
}
