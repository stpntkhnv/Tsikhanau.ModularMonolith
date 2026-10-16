using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Application.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

public sealed class ModuleQueueProcessor<TModule>(
    IQueuePump<TModule> queuePump,
    QueueSignals queueSignals,
    TimeProvider timeProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    IOptions<QueueSettings> options,
    ILogger<ModuleQueueProcessor<TModule>> logger)
    : BackgroundService
    where TModule : IModuleFacade
{
    private Boolean IsStillProcessing => ExecuteTask is { IsCompleted: false };

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (IsStillProcessing)
        {
            PumpLog.AbandonedAtShutdown(logger, typeof(TModule).Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RunInBackground)
        {
            return;
        }

        var reader = queueSignals.GetReader(typeof(TModule));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await queuePump.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (IsStopping(stoppingToken))
            {
                return;
            }
            catch (Exception exception)
            {
                PumpLog.LoopFailed(logger, typeof(TModule).Name, exception);
            }

            await WaitForWorkAsync(reader, stoppingToken);
        }
    }

    private Boolean IsStopping(CancellationToken stoppingToken) =>
        stoppingToken.IsCancellationRequested || hostApplicationLifetime.ApplicationStopping.IsCancellationRequested;

    private async Task WaitForWorkAsync(ChannelReader<Boolean> reader, CancellationToken stoppingToken)
    {
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var signal = reader
            .WaitToReadAsync(cancellationTokenSource.Token)
            .AsTask();

        var tick = Task.Delay(options.Value.PollInterval, timeProvider, cancellationTokenSource.Token);
        await Task.WhenAny(signal, tick);
        await cancellationTokenSource.CancelAsync();
        reader.TryRead(out _);
    }
}
