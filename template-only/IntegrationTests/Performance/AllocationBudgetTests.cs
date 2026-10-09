using System.Diagnostics;
using System.Reflection;
using ModularMonolith.Testing.Database;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.IntegrationTests.Hosting;

namespace Tsikhanau.Monolith.IntegrationTests.Performance;

public sealed class AllocationBudgetTests(PostgresFixture postgresFixture)
{
    private const String DebugBuildReason =
        "Allocation budgets hold for Release builds; in Debug async state machines are classes and allocate more";

    private const Int32 WarmupCommands = 50;
    private const Int32 MeasuredCommands = 200;
    private const Int64 MaxAllocatedBytesPerCommand = 48_000;

    private static Boolean IsReleaseBuild =>
        typeof(UnitOfWork<>).Assembly.GetCustomAttribute<DebuggableAttribute>() is not { IsJITOptimizerDisabled: true };

    [Fact]
    public async Task Create_ping_allocates_on_average_at_most_48_kilobytes_per_command()
    {
        Assert.SkipUnless(IsReleaseBuild, DebugBuildReason);
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture, writesLogs: false);
        await CreatePingsAsync(testModulesHost, "warmup", WarmupCommands);
        var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);

        await CreatePingsAsync(testModulesHost, "measured", MeasuredCommands);

        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
        var allocatedBytesPerCommand = allocatedBytes / MeasuredCommands;

        allocatedBytesPerCommand.ShouldBeLessThanOrEqualTo(MaxAllocatedBytesPerCommand);
    }

    private static async Task CreatePingsAsync(TestModulesHost testModulesHost, String labelPrefix, Int32 count)
    {
        for (var index = 0; index < count; index++)
        {
            await testModulesHost.CreatePingAsync($"{labelPrefix} {index}");
        }
    }
}
