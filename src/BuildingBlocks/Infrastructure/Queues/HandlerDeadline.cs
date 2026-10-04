namespace ModularMonolith.BuildingBlocks.Infrastructure.Queues;

internal sealed class HandlerDeadline : IDisposable
{
    private readonly CancellationTokenSource _timeoutSource;
    private readonly CancellationTokenSource _linkedSource;

    private HandlerDeadline(TimeSpan handlerTimeout, TimeProvider timeProvider, CancellationToken stoppingToken)
    {
        _timeoutSource = new CancellationTokenSource(handlerTimeout, timeProvider);
        _linkedSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _timeoutSource.Token);
    }

    public CancellationToken Token => _linkedSource.Token;

    public Boolean HasPassed => _timeoutSource.IsCancellationRequested;

    public static HandlerDeadline Start(
        TimeSpan handlerTimeout,
        TimeProvider timeProvider,
        CancellationToken stoppingToken) =>
        new(handlerTimeout, timeProvider, stoppingToken);

    public void Dispose()
    {
        _linkedSource.Dispose();
        _timeoutSource.Dispose();
    }
}
