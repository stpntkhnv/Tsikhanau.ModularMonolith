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

        return Answer(integrationEvent.Secret);
    }

    private Result<Unit> Answer(String secret)
    {
        if (pongTestHooks.FailUnexpectedlyFor.Contains(secret))
        {
            return Error.Unexpected("Pong.SecretBroken", $"Pong broke on the secret {secret}");
        }

        return pongTestHooks.FailFor.Contains(secret)
            ? Error.Failure("Pong.SecretRefused", $"Pong refused the secret {secret}")
            : Unit.Value;
    }
}
