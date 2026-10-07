using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Tsikhanau.Railway;

namespace ModularMonolith.Perf.Database;

internal sealed class PerfServer : IAsyncDisposable
{
    private const String ConnectionStringVariable = "PERF_PG";
    private const String Image = "postgres:17-alpine";

    private readonly Optional<PostgreSqlContainer> _container;

    private PerfServer(String connectionString, Optional<PostgreSqlContainer> container)
    {
        ConnectionString = connectionString;
        _container = container;
    }

    public String ConnectionString { get; }

    public static async Task<PerfServer> StartAsync(CancellationToken cancellationToken)
    {
        Optional<String> givenConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        return givenConnectionString.HasValue
            ? new PerfServer(givenConnectionString.Value, Optional<PostgreSqlContainer>.None())
            : await StartContainerAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container.HasValue)
        {
            await _container.Value.DisposeAsync();
        }
    }

    private static async Task<PerfServer> StartContainerAsync(CancellationToken cancellationToken)
    {
        var container = new PostgreSqlBuilder(Image)
            .WithLogger(NullLogger.Instance)
            .WithCommand("-c", "shared_preload_libraries=pg_stat_statements")
            .WithCommand("-c", "pg_stat_statements.track=all")
            .WithCommand("-c", "max_connections=300")
            .Build();

        try
        {
            await container.StartAsync(cancellationToken);
            return new PerfServer(BuildPlainTextConnectionString(container), container);
        }
        catch
        {
            await container.DisposeAsync();
            throw;
        }
    }

    private static String BuildPlainTextConnectionString(PostgreSqlContainer container) =>
        new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            SslMode = SslMode.Disable,
        }.ConnectionString;
}
