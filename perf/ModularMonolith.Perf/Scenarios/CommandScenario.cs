using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Perf.Database;
using ModularMonolith.Perf.Hosting;
using ModularMonolith.Perf.Measuring;
using ModularMonolith.Perf.Running;
using ModularMonolith.TestModules.Pings;

namespace ModularMonolith.Perf.Scenarios;

internal sealed class CommandScenario : IScenario
{
    private const Int32 WarmupConcurrency = 8;

    public String Name => "command";

    public async Task RunAsync(PerfOptions options, CancellationToken cancellationToken)
    {
        var operations = options.GetInt32("n", 3000);
        var warmupOperations = options.GetInt32("warmup", 1000);
        var concurrencies = options.GetInt32List("conc", "1,8,32");
        var repeats = options.GetInt32("repeat", 1);
        await using var database = await PerfDatabase.CreateAsync(cancellationToken);
        using var host = await PerfHost.StartAsync(
            database.ConnectionString,
            options.GetHostSettings(runInBackground: false),
            cancellationToken);

        var pingsModule = host.Services.GetRequiredService<IPingsModule>();
        var pingCreator = new PingCreator();
        await LoadGenerator.RunAsync("warmup", WarmupConcurrency, warmupOperations, CreatePingAsync);
        await LoadGenerator.RunAsync("warmup-seq", 1, warmupOperations / 2, CreatePingAsync);
        for (var repeat = 0; repeat < repeats; repeat++)
        {
            foreach (var concurrency in concurrencies)
            {
                await database.ResetStatementsAsync(cancellationToken);
                var result = await LoadGenerator.RunAsync("create", concurrency, operations, CreatePingAsync);
                Console.WriteLine(result);
                if (repeat == 0 && concurrency == concurrencies[0])
                {
                    await StatementReport.WriteAsync(database, operations, cancellationToken);
                }
            }
        }

        Task CreatePingAsync(Int32 _) => pingCreator.CreateAsync(pingsModule, cancellationToken);
    }
}
