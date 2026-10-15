namespace Tsikhanau.Monolith.TestHost.Probes;

internal sealed class StartRecorder(String recordPath) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(recordPath, nameof(StartAsync), cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
