using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ModularMonolith.BuildingBlocks.Infrastructure.Database;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using Npgsql;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Database;

public sealed class DatabasePreparationTests(PostgresFixture postgresFixture)
{
    private const String AdvisoryLock = "advisory";
    private const String TableLock = "relation";
    private const String AwaitedLocksSql = "SELECT count(*) FROM pg_locks WHERE NOT granted AND locktype = @LockType";
    private const String LockMigrationHistorySql = "LOCK TABLE pings.__ef_migrations_history IN ACCESS EXCLUSIVE MODE";
    private const String TimeoutMessage = "Database preparation did not finish within PrepareTimeout";
    private const String ModuleSchemasSql = "SELECT nspname FROM pg_namespace WHERE nspname IN ('pings', 'pongs')";

    private static readonly TimeSpan RealTimeLimit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockWait = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan MigrationWait = TimeSpan.FromMinutes(5);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preparation_waits_for_the_lock_longer_than_the_command_timeout_of_the_connection_string()
    {
        var connectionString = await CreateDatabaseAsync();
        var oneSecondCommandsConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            CommandTimeout = 1,
        }.ConnectionString;

        using var host = TestModulesHost.BuildUnstarted(oneSecondCommandsConnectionString);
        await using var lockHolder = await HoldPreparationLockAsync(connectionString);
        var preparation = host.Services.PrepareModuleDatabasesAsync(CancellationToken);
        await WaitUntilAwaitingAsync(connectionString, AdvisoryLock, preparation);

        await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken);
        await lockHolder.UnlockAsync(CancellationToken);

        await Should.NotThrowAsync(() => preparation.WaitAsync(RealTimeLimit, CancellationToken));
    }

    [Fact]
    public async Task Lock_wait_and_migrations_share_one_deadline()
    {
        var connectionString = await CreatePreparedDatabaseAsync();
        var fakeTimeProvider = new FakeTimeProvider(TestModulesHost.TestStartedOn);
        using var host = TestModulesHost.BuildUnstarted(connectionString, fakeTimeProvider);
        await using var lockHolder = await HoldPreparationLockAsync(connectionString);
        var preparation = host.Services.PrepareModuleDatabasesAsync(CancellationToken);
        await WaitUntilAwaitingAsync(connectionString, AdvisoryLock, preparation);
        fakeTimeProvider.Advance(LockWait);
        await using var migrationHistoryHolder = await HoldMigrationHistoryAsync(connectionString);
        await lockHolder.UnlockAsync(CancellationToken);
        await WaitUntilAwaitingAsync(connectionString, TableLock, preparation);

        fakeTimeProvider.Advance(MigrationWait);
        var exception = await Should.ThrowAsync<TimeoutException>(
            () => preparation.WaitAsync(RealTimeLimit, CancellationToken));

        LockWait.ShouldBeLessThan(TestModulesHost.PrepareTimeout);
        MigrationWait.ShouldBeLessThan(TestModulesHost.PrepareTimeout);
        exception.Message.ShouldStartWith(TimeoutMessage);
    }

    [Fact]
    public async Task Preparation_lock_is_free_after_the_deadline_passes()
    {
        var connectionString = await CreatePreparedDatabaseAsync();
        var fakeTimeProvider = new FakeTimeProvider(TestModulesHost.TestStartedOn);
        using var host = TestModulesHost.BuildUnstarted(connectionString, fakeTimeProvider);
        var migrationHistoryHolder = await HoldMigrationHistoryAsync(connectionString);
        var timedOutPreparation = host.Services.PrepareModuleDatabasesAsync(CancellationToken);
        await WaitUntilAwaitingAsync(connectionString, TableLock, timedOutPreparation);
        fakeTimeProvider.Advance(TestModulesHost.PrepareTimeout);
        var timeoutException = await Should.ThrowAsync<TimeoutException>(
            () => timedOutPreparation.WaitAsync(RealTimeLimit, CancellationToken));

        await migrationHistoryHolder.DisposeAsync();

        var nextPreparation = PrepareAsync(connectionString);

        timeoutException.Message.ShouldStartWith(TimeoutMessage);
        await Should.NotThrowAsync(() => nextPreparation.WaitAsync(RealTimeLimit, CancellationToken));
    }

    [Fact]
    public async Task Cancellation_by_the_caller_stays_a_cancellation()
    {
        var connectionString = await CreateDatabaseAsync();
        using var host = TestModulesHost.BuildUnstarted(connectionString);
        using var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        await using var lockHolder = await HoldPreparationLockAsync(connectionString);
        var preparation = host.Services.PrepareModuleDatabasesAsync(callerCancellation.Token);
        await WaitUntilAwaitingAsync(connectionString, AdvisoryLock, preparation);

        await callerCancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => preparation.WaitAsync(RealTimeLimit, CancellationToken));
    }

    [Fact]
    public async Task Unreachable_database_stops_preparation_at_once_naming_the_server()
    {
        var closedPort = FindClosedPort();
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = closedPort,
            Database = "modularmonolith",
            Username = "postgres",
            Password = "postgres",
        }.ConnectionString;

        using var host = TestModulesHost.BuildUnstarted(connectionString);

        var preparation = host.Services.PrepareModuleDatabasesAsync(CancellationToken);
        var exception = await Should.ThrowAsync<NpgsqlException>(
            () => preparation.WaitAsync(RealTimeLimit, CancellationToken));

        var socketException = exception.InnerException.ShouldBeOfType<SocketException>();
        exception.Message.ShouldBe($"Failed to connect to 127.0.0.1:{closedPort}");
        socketException.SocketErrorCode.ShouldBe(SocketError.ConnectionRefused);
    }

    [Fact]
    public async Task Waiting_for_the_preparation_lock_is_logged()
    {
        var connectionString = await CreateDatabaseAsync();
        var capturingLoggerProvider = new CapturingLoggerProvider();
        using var host = TestModulesHost.BuildUnstarted(
            connectionString,
            builder => builder.Logging.AddProvider(capturingLoggerProvider));

        await using var lockHolder = await HoldPreparationLockAsync(connectionString);
        var preparation = host.Services.PrepareModuleDatabasesAsync(CancellationToken);
        await WaitUntilAwaitingAsync(connectionString, AdvisoryLock, preparation);

        var messagesWhileWaiting = capturingLoggerProvider.Entries
            .Select(entry => entry.Message)
            .ToList();

        await lockHolder.UnlockAsync(CancellationToken);
        await preparation.WaitAsync(RealTimeLimit, CancellationToken);

        messagesWhileWaiting.ShouldContain("Waiting for the database preparation lock");
    }

    [Fact]
    public async Task Invalid_settings_stop_preparation_before_any_module_schema_exists()
    {
        var connectionString = await CreateDatabaseAsync();
        using var host = TestModulesHost.BuildUnstarted(
            connectionString,
            builder => builder.Configuration["ModularMonolith:Modules:Pings:Limit"] = "0");

        await Should.ThrowAsync<OptionsValidationException>(
            () => host.Services.PrepareModuleDatabasesAsync(CancellationToken));

        var moduleSchemas = await new DatabaseProbe(connectionString).QueryAsync<String>(ModuleSchemasSql);

        moduleSchemas.ShouldBeEmpty();
    }

    private static Task<AdminSession> HoldPreparationLockAsync(String connectionString) =>
        AdminSession.OpenLockedAsync(BuildUnpooled(connectionString), CancellationToken);

    private static async Task<NpgsqlConnection> HoldMigrationHistoryAsync(String connectionString)
    {
        var connection = new NpgsqlConnection(BuildUnpooled(connectionString));
        await connection.OpenAsync(CancellationToken);
        await connection.BeginTransactionAsync(CancellationToken);
        await using var command = new NpgsqlCommand(LockMigrationHistorySql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken);
        return connection;
    }

    private static async Task WaitUntilAwaitingAsync(String connectionString, String lockType, Task preparation)
    {
        var databaseProbe = new DatabaseProbe(connectionString);
        var stopwatch = Stopwatch.StartNew();
        while (!preparation.IsCompleted
            && await databaseProbe.ExecuteScalarAsync<Int64>(AwaitedLocksSql, new { LockType = lockType }) == 0)
        {
            stopwatch.Elapsed.ShouldBeLessThan(RealTimeLimit);
            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken);
        }
    }

    private static async Task PrepareAsync(String connectionString)
    {
        using var host = TestModulesHost.BuildUnstarted(connectionString);
        await host.Services.PrepareModuleDatabasesAsync(CancellationToken);
    }

    private static String BuildUnpooled(String connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        }.ConnectionString;

    private static Int32 FindClosedPort()
    {
        using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        tcpListener.Stop();
        return port;
    }

    private async Task<String> CreatePreparedDatabaseAsync()
    {
        var connectionString = await CreateDatabaseAsync();
        await PrepareAsync(connectionString);
        return connectionString;
    }

    private async Task<String> CreateDatabaseAsync()
    {
        var databaseName = $"preparation_{Guid.CreateVersion7():N}";
        await new DatabaseProbe(postgresFixture.ConnectionString).ExecuteAsync($"CREATE DATABASE {databaseName}");
        return new NpgsqlConnectionStringBuilder(postgresFixture.ConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;
    }
}
