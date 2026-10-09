using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Perf.Database;
using Tsikhanau.Monolith.Perf.Hosting;
using Tsikhanau.Monolith.Perf.Measuring;
using Tsikhanau.Monolith.Perf.Running;
using Tsikhanau.Monolith.TestModules.Pings;

namespace Tsikhanau.Monolith.Perf.Scenarios;

internal sealed class QueryScenario : IScenario
{
    private const Int32 SeededPings = 1000;
    private const Int32 SeedConcurrency = 8;
    private const Int32 WarmupOperations = 2000;
    private const Int32 WarmupConcurrency = 8;

    public String Name => "query";

    public async Task RunAsync(PerfOptions options, CancellationToken cancellationToken)
    {
        var operations = options.GetInt32("n", 5000);
        var concurrencies = options.GetInt32List("conc", "1,8");
        var repeats = options.GetInt32("repeat", 1);
        await using var database = await PerfDatabase.CreateAsync(cancellationToken);
        using var host = await PerfHost.StartAsync(
            database.ConnectionString,
            options.GetHostSettings(runInBackground: false),
            cancellationToken);

        var pingsModule = host.Services.GetRequiredService<IPingsModule>();
        var pingCreator = new PingCreator();
        var pingIds = new Guid[SeededPings];
        await LoadGenerator.RunAsync("seed", SeedConcurrency, pingIds.Length, SeedPingAsync);
        await LoadGenerator.RunAsync("warmup", WarmupConcurrency, WarmupOperations, GetPingAsync);
        for (var repeat = 0; repeat < repeats; repeat++)
        {
            foreach (var concurrency in concurrencies)
            {
                await database.ResetStatementsAsync(cancellationToken);
                var result = await LoadGenerator.RunAsync("query-facade", concurrency, operations, GetPingAsync);
                Console.WriteLine(result);
                if (repeat == 0 && concurrency == concurrencies[0])
                {
                    await StatementReport.WriteAsync(database, operations, cancellationToken);
                }
            }
        }

        async Task SeedPingAsync(Int32 index) =>
            pingIds[index] = await pingCreator.CreateAsync(pingsModule, cancellationToken);

        Task GetPingAsync(Int32 index) => pingsModule.GetPingAsync(pingIds[index % pingIds.Length], cancellationToken);
    }
}
