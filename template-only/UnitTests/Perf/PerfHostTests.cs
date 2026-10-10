using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Perf.Hosting;
using Tsikhanau.Monolith.Perf.Running;

namespace Tsikhanau.Monolith.UnitTests.Perf;

public sealed class PerfHostTests
{
    private const String ConnectionString =
        "Host=127.0.0.1;Port=1;Database=perf;Username=postgres;Password=postgres;SSL Mode=Disable";

    [Fact]
    public void Perf_host_adds_the_test_modules_through_the_module_path()
    {
        using var host = BuildHost();

        var moduleNames = host.Services
            .GetServices<IModule>()
            .Select(module => module.Name);

        moduleNames.ShouldBe(["Pings", "Pongs"]);
    }

    [Fact]
    public void Perf_host_configuration_passes_the_startup_validation()
    {
        using var host = BuildHost();

        var startupValidator = host.Services.GetRequiredService<IStartupValidator>();

        Should.NotThrow(startupValidator.Validate);
    }

    private static IHost BuildHost()
    {
        var hostSettings = PerfOptions
            .Parse([])
            .GetHostSettings(runInBackground: false);

        return PerfHost.Build(ConnectionString, hostSettings);
    }
}
