using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tsikhanau.Monolith.Testing.App;
using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal static class TestHostEnvironment
{
    public const String UrlsKey = "ASPNETCORE_URLS";

    public static Dictionary<String, String> Create(String connectionString) =>
        Create(connectionString, Environments.Production);

    public static Dictionary<String, String> Create(String connectionString, String environmentName) =>
        new()
        {
            ["ASPNETCORE_ENVIRONMENT"] = environmentName,
            ["DOTNET_ENVIRONMENT"] = environmentName,
            [UrlsKey] = BuildUrl(GetFreePort()),
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = String.Empty,
            ["ConnectionStrings__database"] = connectionString,
            ["App__Database__LoginKey"] = TestSettings.LoginKey,
            ["App__Database__PrepareMode"] = "OnStartup",
            ["App__Queues__RunInBackground"] = "true",
            ["App__Cleanup__RunInBackground"] = "true",
            ["App__Modules__Pings__Limit"] = "10",
            ["App__Modules__Pongs__Limit"] = "10",
        };

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
