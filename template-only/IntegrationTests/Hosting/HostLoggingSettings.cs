using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tsikhanau.Monolith.Testing.Modules;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal static class HostLoggingSettings
{
    private static readonly String HostSettingsPath = Path.Combine(GetHostFolder(), "appsettings.json");

    public static ILoggingBuilder ApplyTo(ILoggingBuilder loggingBuilder)
    {
        loggingBuilder.Services.Configure<LoggerFilterOptions>(ClearRules);
        return loggingBuilder.AddConfiguration(ReadLoggingSection());
    }

    private static void ClearRules(LoggerFilterOptions loggerFilterOptions) => loggerFilterOptions.Rules.Clear();

    private static IConfigurationSection ReadLoggingSection() =>
        new ConfigurationBuilder()
            .AddJsonFile(HostSettingsPath, optional: false)
            .Build()
            .GetSection("Logging");

    private static String GetHostFolder() =>
        Directory
            .GetDirectories(Path.Combine(SolutionPaths.Root, "src", "Bootstrapper"), "*.Bootstrapper")
            .Single();
}
