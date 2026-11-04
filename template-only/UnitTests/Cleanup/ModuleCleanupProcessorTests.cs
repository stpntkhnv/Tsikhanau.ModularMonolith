using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Infrastructure.Cleanup;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.UnitTests.Time;

namespace Tsikhanau.Monolith.UnitTests.Cleanup;

public sealed class ModuleCleanupProcessorTests
{
    private static readonly CleanupSettings Settings = new()
    {
        Interval = TimeSpan.FromHours(1),
        KeepFor = TimeSpan.FromDays(7),
    };

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stop_while_the_cleanup_waits_for_its_next_round_ends_its_loop_without_an_exception()
    {
        var timerSignallingTimeProvider = new TimerSignallingTimeProvider();
        await using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        using var moduleCleanupProcessor = CreateProcessor(serviceProvider, timerSignallingTimeProvider);

        await moduleCleanupProcessor.StartAsync(CancellationToken);
        await timerSignallingTimeProvider.TimerCreated.WaitAsync(Deadline, CancellationToken);
        await moduleCleanupProcessor.StopAsync(CancellationToken);

        var executionStatus = moduleCleanupProcessor.ExecuteTask?.Status;

        executionStatus.ShouldBe(TaskStatus.RanToCompletion);
    }

    private static ModuleCleanupProcessor<IPingsModule> CreateProcessor(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider)
    {
        var options = Options.Create(Settings);
        var moduleCleanup = new ModuleCleanup<IPingsModule>(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            options,
            timeProvider);

        return new(moduleCleanup, timeProvider, options, NullLogger<ModuleCleanupProcessor<IPingsModule>>.Instance);
    }
}
