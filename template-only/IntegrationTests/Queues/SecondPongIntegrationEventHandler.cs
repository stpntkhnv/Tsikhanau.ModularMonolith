using Microsoft.EntityFrameworkCore;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

internal sealed class SecondPongIntegrationEventHandler(
    IPongRepository pongRepository,
    PongsDbContext pongsDbContext,
    SecondPongHandlerHooks secondPongHandlerHooks,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<PingCreatedIntegrationEvent>
{
    private const String FailingSql = "DO $$ BEGIN RAISE EXCEPTION 'second pong handler failed'; END $$";

    public Task<Result<Unit>> Handle(
        PingCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        secondPongHandlerHooks.ReceivedPingIds.Enqueue(integrationEvent.PingId);
        return PongLabel
            .Create(integrationEvent.Label, "label")
            .Map(label => CreatePong(integrationEvent.PingId, label))
            .Tap(pongRepository.Add)
            .BindAsync(_ => FlushThenFailIfAskedAsync(cancellationToken));
    }

    private Pong CreatePong(Guid pingId, PongLabel label)
    {
        var createdOn = timeProvider.GetUtcNow();
        return Pong.Create(PongId.From(Guid.CreateVersion7(createdOn)), pingId, label, createdOn);
    }

    private async Task<Result<Unit>> FlushThenFailIfAskedAsync(CancellationToken cancellationToken)
    {
        await pongsDbContext.SaveChangesAsync(cancellationToken);
        if (secondPongHandlerHooks.FailsNextCall)
        {
            secondPongHandlerHooks.FailsNextCall = false;
            await pongsDbContext.Database.ExecuteSqlRawAsync(FailingSql, cancellationToken);
        }

        return Unit.Value;
    }
}
