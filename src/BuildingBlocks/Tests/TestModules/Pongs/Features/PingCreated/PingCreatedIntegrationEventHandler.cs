using System.Diagnostics;
using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.TestModules.Pings.Contracts.IntegrationEvents;
using ModularMonolith.TestModules.Pongs.Domain.Pongs;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pongs.Features.PingCreated;

internal sealed class PingCreatedIntegrationEventHandler(
    IPongRepository pongRepository,
    PongTestHooks pongTestHooks,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<PingCreatedIntegrationEvent>
{
    public static readonly Error Rejected = Error.Failure("Pong.Rejected", "Pong refused this ping");

    public static readonly Error Broken = Error.Unexpected("Pong.Broken", "Pong broke on this ping");

    public async Task<Result<Unit>> Handle(
        PingCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        pongTestHooks.Record(integrationEvent.PingId);
        await pongTestHooks.BeforeHandle();
        if (pongTestHooks.TerminateConnectionFor.Contains(integrationEvent.Label))
        {
            await pongRepository.TerminateConnectionAsync(cancellationToken);
        }

        if (pongTestHooks.KeepBusyFor.Contains(integrationEvent.Label))
        {
            await KeepTransactionBusyAsync(PongId.From(integrationEvent.PingId));
        }

        var pongId = PongId.From(integrationEvent.PingId);
        if (!await pongRepository.ExistsAsync(pongId, cancellationToken))
        {
            pongRepository.Add(
                Pong.Create(pongId, integrationEvent.PingId, integrationEvent.Label, timeProvider.GetUtcNow()));
        }

        if (pongTestHooks.CancelFor.Contains(integrationEvent.Label))
        {
            await pongTestHooks.Cancellation.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (pongTestHooks.ThrowFor.Contains(integrationEvent.Label))
        {
            throw new InvalidOperationException($"Pong handler failed for {integrationEvent.Label}");
        }

        if (pongTestHooks.RejectAsInvalidFor.Contains(integrationEvent.Label))
        {
            return Error.Validation("label", "Pong refused this label");
        }

        if (pongTestHooks.FailUnexpectedlyFor.Contains(integrationEvent.Label))
        {
            return Broken;
        }

        return pongTestHooks.FailFor.Contains(integrationEvent.Label) ? Rejected : Unit.Value;
    }

    private async Task KeepTransactionBusyAsync(PongId pongId)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            await pongRepository.ExistsAsync(pongId, CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
    }
}
