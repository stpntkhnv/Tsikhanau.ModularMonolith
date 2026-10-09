using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Application.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Cleanup;

public sealed class ModuleCleanup<TModule>(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<CleanupSettings> options,
    TimeProvider timeProvider)
    : IModuleCleanup
    where TModule : IModuleFacade
{
    public async Task<Int32> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var deleteBefore = timeProvider.GetUtcNow() - options.Value.KeepFor;
        var deletedCount = 0;
        foreach (var cleanupTask in scope.ServiceProvider.GetServices<ICleanupTask<TModule>>())
        {
            deletedCount += await cleanupTask.DeleteExpiredAsync(deleteBefore, cancellationToken);
        }

        return deletedCount;
    }
}
