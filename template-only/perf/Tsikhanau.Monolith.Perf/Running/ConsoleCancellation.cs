namespace Tsikhanau.Monolith.Perf.Running;

internal sealed class ConsoleCancellation : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public ConsoleCancellation() => Console.CancelKeyPress += Cancel;

    public CancellationToken Token => _cancellationTokenSource.Token;

    public void Dispose()
    {
        Console.CancelKeyPress -= Cancel;
        _cancellationTokenSource.Dispose();
    }

    private void Cancel(Object? sender, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        _cancellationTokenSource.Cancel();
    }
}
