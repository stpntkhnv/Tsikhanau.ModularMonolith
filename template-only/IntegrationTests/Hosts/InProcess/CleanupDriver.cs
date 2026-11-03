using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Cleanup;

namespace Tsikhanau.Monolith.IntegrationTests.Hosts.InProcess;

internal static class CleanupDriver
{
    public static async Task<Int32> RunAllAsync(IServiceProvider serviceProvider)
    {
        var deletedCount = 0;
        foreach (var moduleCleanup in serviceProvider.GetServices<IModuleCleanup>())
        {
            deletedCount += await moduleCleanup.RunOnceAsync(TestContext.Current.CancellationToken);
        }

        return deletedCount;
    }
}
