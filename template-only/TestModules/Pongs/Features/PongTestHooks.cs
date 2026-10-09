using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pongs.Features;

internal sealed class PongTestHooks
{
    public ImmutableHashSet<String> ThrowFor { get; set; } = [];

    public ImmutableHashSet<String> FailFor { get; set; } = [];

    public ImmutableHashSet<String> FailUnexpectedlyFor { get; set; } = [];

    public ImmutableHashSet<String> RejectAsInvalidFor { get; set; } = [];

    public ImmutableHashSet<String> CancelFor { get; set; } = [];

    public ImmutableHashSet<String> TerminateConnectionFor { get; set; } = [];

    public ImmutableHashSet<String> KeepBusyFor { get; set; } = [];

    public ImmutableHashSet<String> ChangeStateFor { get; set; } = [];

    public CancellationTokenSource Cancellation { get; set; } = new();

    public Func<Task> BeforeHandle { get; set; } = static () => Task.CompletedTask;

    public Func<CancellationToken, Task> WhileHandlingSecret { get; set; } = static _ => Task.CompletedTask;

    public ConcurrentQueue<Guid> ReceivedPingIds { get; } = new();

    public ConcurrentQueue<String> ReceivedSecrets { get; } = new();

    public Optional<ActivityTraceId> LastTraceId { get; private set; }

    public void Record(Guid pingId)
    {
        ReceivedPingIds.Enqueue(pingId);
        LastTraceId = Optional<Activity>
            .FromNullable(Activity.Current)
            .Map(activity => activity.TraceId);
    }
}
