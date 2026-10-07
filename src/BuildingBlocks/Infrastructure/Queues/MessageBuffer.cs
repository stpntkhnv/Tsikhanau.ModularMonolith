using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.Shared.Collections;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Queues;

internal abstract class MessageBuffer : IAsyncDisposable
{
    protected abstract Boolean HasMessages { get; }

    private Boolean IsInRound { get; set; }

    public static async Task<Int32> ProcessInRoundsAsync(
        IReadOnlyList<MessageBuffer> messageBuffers,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (true)
        {
            var processedCount = await ProcessRoundAsync(messageBuffers, cancellationToken);
            if (processedCount == 0)
            {
                return total;
            }

            total += processedCount;
        }
    }

    public abstract ValueTask DisposeAsync();

    protected abstract Task ClaimAsync(CancellationToken cancellationToken);

    protected abstract Task<Boolean> TryProcessNextAsync(CancellationToken cancellationToken);

    private static async Task<Int32> ProcessRoundAsync(
        IReadOnlyList<MessageBuffer> messageBuffers,
        CancellationToken cancellationToken)
    {
        messageBuffers.ForEach(messageBuffer => messageBuffer.IsInRound = true);
        var processedCount = 0;
        while (messageBuffers.Any(messageBuffer => messageBuffer.IsInRound))
        {
            foreach (var messageBuffer in messageBuffers.Where(messageBuffer => messageBuffer.IsInRound))
            {
                processedCount += Count(await messageBuffer.TakeTurnAsync(cancellationToken));
            }
        }

        return processedCount;
    }

    private static Int32 Count(Boolean isProcessed) => isProcessed ? 1 : 0;

    private async Task<Boolean> TakeTurnAsync(CancellationToken cancellationToken)
    {
        if (!HasMessages)
        {
            await ClaimAsync(cancellationToken);
        }

        var isProcessed = HasMessages && await TryProcessNextAsync(cancellationToken);
        IsInRound = HasMessages;
        return isProcessed;
    }
}

internal sealed class MessageBuffer<TModule, TMessage>(
    MessagePump<TModule, TMessage> messagePump,
    QueueSettings queueSettings,
    TimeProvider timeProvider)
    : MessageBuffer
    where TModule : IModuleFacade
    where TMessage : QueueMessage
{
    private const String ShortLease = "under 1.5 x HandlerTimeout of the lease left";

    private const String OldBatch = "older than PollInterval";

    private readonly Queue<TMessage> _messages = new();
    private DateTimeOffset _claimedOn;
    private DateTimeOffset _claimsOneAtATimeUntil;
    private Boolean _claimsOneAtATime;

    protected override Boolean HasMessages => _messages.Count > 0;

    private Int32 ClaimBatchSize => _claimsOneAtATime ? 1 : queueSettings.ClaimBatchSize;

    private TimeSpan LeaseNeededToStart => queueSettings.HandlerTimeout * 1.5;

    public override async ValueTask DisposeAsync()
    {
        if (HasMessages)
        {
            await messagePump.GiveBackAsync(TakeAll());
        }
    }

    protected override async Task ClaimAsync(CancellationToken cancellationToken)
    {
        _claimsOneAtATime = timeProvider.GetUtcNow() < _claimsOneAtATimeUntil;
        var claimedMessages = await messagePump.ClaimBatchAsync(ClaimBatchSize, cancellationToken);
        _claimedOn = timeProvider.GetUtcNow();
        claimedMessages.ForEach(_messages.Enqueue);
    }

    protected override async Task<Boolean> TryProcessNextAsync(CancellationToken cancellationToken)
    {
        var nextMessage = _messages.Peek();
        var startedOn = timeProvider.GetUtcNow();
        if (!CanStart(nextMessage, startedOn))
        {
            ClaimOneAtATimeForAPollInterval();
            await messagePump.GiveBackUnstartedAsync(TakeAll(), BuildReasonNotToStart(nextMessage, startedOn));
            return false;
        }

        var message = _messages.Dequeue();
        await messagePump.ProcessClaimedAsync(message, cancellationToken);
        var finishedOn = timeProvider.GetUtcNow();
        if (!CanStart(message, finishedOn))
        {
            ClaimOneAtATimeForAPollInterval();
            LogSwitchWithNothingToGiveBack(message, finishedOn);
        }

        return true;
    }

    private void LogSwitchWithNothingToGiveBack(TMessage message, DateTimeOffset finishedOn)
    {
        if (!_claimsOneAtATime && !HasMessages)
        {
            messagePump.LogClaimingOneAtATime(BuildReasonNotToStart(message, finishedOn));
        }
    }

    private void ClaimOneAtATimeForAPollInterval() =>
        _claimsOneAtATimeUntil = timeProvider.GetUtcNow() + queueSettings.PollInterval;

    private Boolean CanStart(TMessage message, DateTimeOffset startedOn) =>
        HasLeaseToStart(message, startedOn) && IsFresh(startedOn);

    private Boolean HasLeaseToStart(TMessage message, DateTimeOffset startedOn) =>
        message.NextAttemptOn - startedOn >= LeaseNeededToStart;

    private Boolean IsFresh(DateTimeOffset startedOn) => startedOn - _claimedOn <= queueSettings.PollInterval;

    private String BuildReasonNotToStart(TMessage message, DateTimeOffset startedOn) =>
        (HasLeaseToStart(message, startedOn), IsFresh(startedOn)) switch
        {
            (false, false) => $"{ShortLease}, {OldBatch}",
            (false, true) => ShortLease,
            _ => OldBatch,
        };

    private List<TMessage> TakeAll()
    {
        var messages = _messages.ToList();
        _messages.Clear();
        return messages;
    }
}
