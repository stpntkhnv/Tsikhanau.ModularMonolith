using Microsoft.Extensions.Hosting;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class StopBarrier : IHostedService
{
    private readonly TaskCompletionSource _releaseSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => _releaseSource.Task.WaitAsync(cancellationToken);

    public void Release() => _releaseSource.TrySetResult();
}
