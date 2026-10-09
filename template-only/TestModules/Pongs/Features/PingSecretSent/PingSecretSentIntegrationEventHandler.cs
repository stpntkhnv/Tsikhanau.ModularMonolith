using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pongs.Features.PingSecretSent;

internal sealed class PingSecretSentIntegrationEventHandler(
    IPongRepository pongRepository,
    PongTestHooks pongTestHooks,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<PingSecretSentIntegrationEvent>, IRunOutsideTransaction
{
    public static readonly Error Refused = Error.Failure("Pong.SecretRefused", "Pong refused this secret");

    public async Task<Result<Unit>> Handle(
        PingSecretSentIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        pongTestHooks.ReceivedSecrets.Enqueue(integrationEvent.Secret);
        await pongTestHooks.WhileHandlingSecret(cancellationToken);
        if (pongTestHooks.ChangeStateFor.Contains(integrationEvent.Secret))
        {
            pongRepository.Add(
                Pong.Create(
                    PongId.From(integrationEvent.PingId),
                    integrationEvent.PingId,
                    PongLabel.Secret,
                    timeProvider.GetUtcNow()));
        }

        return pongTestHooks.FailFor.Contains(integrationEvent.Secret) ? Refused : Unit.Value;
    }
}
