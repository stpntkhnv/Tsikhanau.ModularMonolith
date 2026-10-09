using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Perf.Database;
using Tsikhanau.Monolith.Perf.Hosting;
using Tsikhanau.Monolith.Perf.Measuring;
using Tsikhanau.Monolith.Perf.Running;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure;

namespace Tsikhanau.Monolith.Perf.Scenarios;

internal sealed class StartupScenario : IScenario
{
    public String Name => "startup";

    public async Task RunAsync(PerfOptions options, CancellationToken cancellationToken)
    {
        await using var database = await PerfDatabase.CreateAsync(cancellationToken);
        var hostSettings = options.GetHostSettings(runInBackground: false);
        var stepTimer = new StepTimer();
        using var host = await StartStepByStepAsync(database, hostSettings, stepTimer, cancellationToken);
        var readyAfter = stepTimer.Total;
        await SendFirstRequestsAsync(host.Services.GetRequiredService<IPingsModule>(), stepTimer, cancellationToken);
        Console.WriteLine(
            $"  total to ready {readyAfter.TotalMilliseconds:F0} ms, "
                + $"to first warm request {stepTimer.Total.TotalMilliseconds:F0} ms, load {MachineLoad.Read()}");

        var preparedAgainAfter = await PrepareAgainAsync(database, hostSettings, cancellationToken);
        Console.WriteLine(
            "  PrepareModuleDatabasesAsync on an already prepared db (same process, model cached): "
                + $"{preparedAgainAfter.TotalMilliseconds:F0} ms");

        await host.StopAsync(cancellationToken);
    }

    private static async Task<IHost> StartStepByStepAsync(
        PerfDatabase database,
        PerfHostSettings hostSettings,
        StepTimer stepTimer,
        CancellationToken cancellationToken)
    {
        _ = new ServiceCollection()
            .AddPingsInfrastructure()
            .AddPongsInfrastructure();

        stepTimer.Mark("module registration (scan + validate, 2 modules)");
        var host = PerfHost.Build(database.ConnectionString, hostSettings);
        stepTimer.Mark("host build (second registration + Build)");
        BuildModels(host.Services, stepTimer);
        await host.Services.PrepareModuleDatabasesAsync(cancellationToken);
        stepTimer.Mark("PrepareModuleDatabasesAsync");
        await host.StartAsync(cancellationToken);
        stepTimer.Mark("host.StartAsync");
        return host;
    }

    private static void BuildModels(IServiceProvider serviceProvider, StepTimer stepTimer)
    {
        using var scope = serviceProvider.CreateScope();
        foreach (var module in serviceProvider.GetRequiredService<ModuleRegistry>().Modules)
        {
            _ = ResolveDbContext(scope.ServiceProvider, module).Model;
            stepTimer.Mark($"{module.Name}: EF model build");
        }
    }

    private static DbContext ResolveDbContext(IServiceProvider serviceProvider, ModuleDescriptor module) =>
        (DbContext)serviceProvider.GetRequiredService(module.DbContextType);

    private static async Task SendFirstRequestsAsync(
        IPingsModule pingsModule,
        StepTimer stepTimer,
        CancellationToken cancellationToken)
    {
        var pingCreator = new PingCreator();
        var pingId = await pingCreator.CreateAsync(pingsModule, cancellationToken);
        stepTimer.Mark("first CreatePing (cold)");
        await pingCreator.CreateAsync(pingsModule, cancellationToken);
        stepTimer.Mark("second CreatePing");
        await pingsModule.GetPingAsync(pingId, cancellationToken);
        stepTimer.Mark("first GetPing (cold)");
        await pingsModule.GetPingAsync(pingId, cancellationToken);
        stepTimer.Mark("second GetPing");
    }

    private static async Task<TimeSpan> PrepareAgainAsync(
        PerfDatabase database,
        PerfHostSettings hostSettings,
        CancellationToken cancellationToken)
    {
        using var host = PerfHost.Build(database.ConnectionString, hostSettings);
        var startedOn = Stopwatch.GetTimestamp();
        await host.Services.PrepareModuleDatabasesAsync(cancellationToken);
        return Stopwatch.GetElapsedTime(startedOn);
    }
}
