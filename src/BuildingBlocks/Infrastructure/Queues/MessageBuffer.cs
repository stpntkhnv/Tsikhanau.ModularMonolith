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
    private readonly Queue<TMessage> _messages = new();
    private DateTimeOffset _claimedOn;
    private Boolean _claimsOneAtATime;

    protected override Boolean HasMessages => _messages.Count > 0;

    private Int32 ClaimBatchSize => _claimsOneAtATime ? 1 : queueSettings.ClaimBatchSize;

    private TimeSpan LeaseNeededToStart => queueSettings.HandlerTimeout * 1.5;

    public override async ValueTask DisposeAsync()
    {
        if (HasMessages)
        {
            await GiveBackAsync();
        }
    }

    protected override async Task ClaimAsync(CancellationToken cancellationToken)
    {
        var claimedMessages = await messagePump.ClaimBatchAsync(ClaimBatchSize, cancellationToken);
        _claimedOn = timeProvider.GetUtcNow();
        claimedMessages.ForEach(_messages.Enqueue);
    }

    protected override async Task<Boolean> TryProcessNextAsync(CancellationToken cancellationToken)
    {
        if (!CanStart(_messages.Peek(), timeProvider.GetUtcNow()))
        {
            _claimsOneAtATime = true;
            await GiveBackAsync();
            return false;
        }

        await messagePump.ProcessClaimedAsync(_messages.Dequeue(), cancellationToken);
        return true;
    }

    private Boolean CanStart(TMessage message, DateTimeOffset startedOn) =>
        message.NextAttemptOn - startedOn >= LeaseNeededToStart
        && startedOn - _claimedOn <= queueSettings.PollInterval;

    private Task GiveBackAsync()
    {
        var messages = _messages.ToList();
        _messages.Clear();
        return messagePump.GiveBackAsync(messages);
    }
}
