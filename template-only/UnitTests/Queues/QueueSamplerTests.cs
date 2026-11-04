using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.Infrastructure.Queues.Metrics;
using Tsikhanau.Monolith.UnitTests.Time;

namespace Tsikhanau.Monolith.UnitTests.Queues;

public sealed class QueueSamplerTests
{
    private static readonly QueueSettings Settings = new()
    {
        PollInterval = TimeSpan.FromSeconds(5),
        HandlerTimeout = TimeSpan.FromMinutes(1),
        ClaimBatchSize = 16,
    };

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stop_while_the_sampler_waits_for_its_next_sample_ends_its_loop_without_an_exception()
    {
        var timerSignallingTimeProvider = new TimerSignallingTimeProvider();
        await using var serviceProvider = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider();

        using var queueSampler = CreateSampler(serviceProvider, timerSignallingTimeProvider);

        await queueSampler.StartAsync(CancellationToken);
        await timerSignallingTimeProvider.TimerCreated.WaitAsync(Deadline, CancellationToken);
        await queueSampler.StopAsync(CancellationToken);

        var executionStatus = queueSampler.ExecuteTask?.Status;

        executionStatus.ShouldBe(TaskStatus.RanToCompletion);
    }

    private static QueueSampler CreateSampler(IServiceProvider serviceProvider, TimeProvider timeProvider)
    {
        var moduleRegistry = new ModuleRegistry();
        var queueMetrics = new QueueMetrics(
            serviceProvider.GetRequiredService<IMeterFactory>(),
            moduleRegistry,
            timeProvider);

        return new(
            moduleRegistry,
            serviceProvider,
            queueMetrics,
            timeProvider,
            Options.Create(Settings),
            NullLogger<QueueSampler>.Instance);
    }
}
