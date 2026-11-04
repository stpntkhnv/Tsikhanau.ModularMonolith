using Microsoft.Extensions.Time.Testing;

namespace Tsikhanau.Monolith.UnitTests.Time;

internal sealed class TimerSignallingTimeProvider : FakeTimeProvider
{
    private readonly TaskCompletionSource _timerCreatedSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task TimerCreated => _timerCreatedSource.Task;

    public override ITimer CreateTimer(TimerCallback callback, Object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        _timerCreatedSource.TrySetResult();
        return timer;
    }
}
