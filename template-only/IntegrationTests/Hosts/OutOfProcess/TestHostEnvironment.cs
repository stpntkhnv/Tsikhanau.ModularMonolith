using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Database.Settings;
using Tsikhanau.Monolith.IntegrationTests.Modules.Pings;
using Tsikhanau.Monolith.IntegrationTests.Modules.Pongs;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.Testing.App;
using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Hosts.OutOfProcess;

internal static class TestHostEnvironment
{
    public const String UrlsKey = "ASPNETCORE_URLS";

    private static IEnumerable<KeyValuePair<String, String>> ModuleSettings =>
        [.. new PingsTestSetup().Settings, .. new PongsTestSetup().Settings];

    public static Dictionary<String, String> Create(String connectionString) =>
        Create(connectionString, Environments.Production);

    public static Dictionary<String, String> Create(String connectionString, String environmentName)
    {
        var environment = new Dictionary<String, String>
        {
            ["ASPNETCORE_ENVIRONMENT"] = environmentName,
            ["DOTNET_ENVIRONMENT"] = environmentName,
            [UrlsKey] = BuildUrl(GetFreePort()),
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = String.Empty,
            [TestSettings.ConnectionStringVariable] = connectionString,
            [TestSettings.BuildEnvironmentVariableName(DatabaseSettings.LoginKeySetting)] = TestSettings.LoginKey,
            [TestSettings.BuildEnvironmentVariableName(DatabaseSettings.PrepareModeSetting)] = "OnStartup",
            [TestSettings.BuildEnvironmentVariableName(TestSettings.QueuesInBackgroundSetting)] = "true",
            [TestSettings.BuildEnvironmentVariableName(TestSettings.CleanupInBackgroundSetting)] = "true",
        };

        ModuleSettings.ForEach(
            setting => environment[TestSettings.BuildEnvironmentVariableName(setting.Key)] = setting.Value);

        return environment;
    }

    public static async Task<String> CreateDatabaseAsync(String serverConnectionString, String prefix)
    {
        var databaseName = $"{prefix}_{Guid.CreateVersion7():N}";
        await new DatabaseProbe(serverConnectionString).ExecuteAsync($"CREATE DATABASE {databaseName}");
        return new NpgsqlConnectionStringBuilder(serverConnectionString)
        {
            Database = databaseName,
            SslMode = SslMode.Disable,
        }.ConnectionString;
    }

    public static String BuildUrl(Int32 port) => $"http://127.0.0.1:{port}";

    public static Int32 GetFreePort()
    {
        using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        return ((IPEndPoint)tcpListener.LocalEndpoint).Port;
    }
}
