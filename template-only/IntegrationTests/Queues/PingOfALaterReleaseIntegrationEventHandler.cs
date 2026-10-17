using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

internal sealed class PingOfALaterReleaseIntegrationEventHandler(
    IPongRepository pongRepository,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<PingOfALaterReleaseIntegrationEvent>
{
    public Task<Result<Unit>> Handle(
        PingOfALaterReleaseIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        Task.FromResult(
            PongLabel
                .Create(integrationEvent.Label, "label")
                .Map(label => CreatePong(integrationEvent.PingId, label))
                .Tap(pongRepository.Add)
                .Map(_ => Unit.Value));

    private Pong CreatePong(Guid pingId, PongLabel label)
    {
        var createdOn = timeProvider.GetUtcNow();
        return Pong.Create(PongId.From(Guid.CreateVersion7(createdOn)), pingId, label, createdOn);
    }
}
