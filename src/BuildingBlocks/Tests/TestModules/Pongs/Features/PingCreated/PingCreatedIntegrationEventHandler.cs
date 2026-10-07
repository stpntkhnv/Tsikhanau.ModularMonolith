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

        return await PongLabel
            .Create(integrationEvent.Label, "label")
            .TapAsync(label => AddPongOnceAsync(integrationEvent.PingId, label, cancellationToken))
            .BindAsync(_ => AnswerAsync(integrationEvent.Label, cancellationToken));
    }

    private async Task AddPongOnceAsync(Guid pingId, PongLabel label, CancellationToken cancellationToken)
    {
        var pongId = PongId.From(pingId);
        if (!await pongRepository.ExistsAsync(pongId, cancellationToken))
        {
            pongRepository.Add(Pong.Create(pongId, pingId, label, timeProvider.GetUtcNow()));
        }
    }

    private async Task<Result<Unit>> AnswerAsync(String label, CancellationToken cancellationToken)
    {
        if (pongTestHooks.CancelFor.Contains(label))
        {
            await pongTestHooks.Cancellation.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (pongTestHooks.ThrowFor.Contains(label))
        {
            throw new InvalidOperationException($"Pong handler failed for {label}");
        }

        if (pongTestHooks.RejectAsInvalidFor.Contains(label))
        {
            return Error.Validation("label", "Pong refused this label");
        }

        if (pongTestHooks.FailUnexpectedlyFor.Contains(label))
        {
            return Broken;
        }

        return pongTestHooks.FailFor.Contains(label) ? Rejected : Unit.Value;
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
