using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Application.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Cleanup;

public sealed class ModuleCleanupProcessor<TModule>(
    ModuleCleanup<TModule> moduleCleanup,
    TimeProvider timeProvider,
    IOptions<CleanupSettings> options,
    ILogger<ModuleCleanupProcessor<TModule>> logger)
    : BackgroundService
    where TModule : IModuleFacade
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RunInBackground)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await moduleCleanup.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                CleanupLog.Failed(logger, typeof(TModule).Name, exception);
            }

            try
            {
                await Task.Delay(options.Value.Interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
