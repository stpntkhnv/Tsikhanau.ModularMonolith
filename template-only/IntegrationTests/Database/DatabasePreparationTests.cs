using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Database;

public sealed class DatabasePreparationTests(PostgresFixture postgresFixture)
{
    private const String AdvisoryLock = "advisory";
    private const String TableLock = "relation";
    private const String AwaitedLocksSql = "SELECT count(*) FROM pg_locks WHERE NOT granted AND locktype = @LockType";
    private const String LockMigrationHistorySql = "LOCK TABLE pings.__ef_migrations_history IN ACCESS EXCLUSIVE MODE";
    private const String TimeoutMessage = "Database preparation did not finish within PrepareTimeout";
    private const Int32 AdminConnectionCount = 2;
    private const String ModuleSchemasSql = "SELECT nspname FROM pg_namespace WHERE nspname IN ('pings', 'pongs')";
    private const String UpgradeNotes = "the upgrade notes in docs/engineering-principles.md, section 5.6";

    private static readonly TimeSpan RealTimeLimit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SilenceLimit = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ClosingLimit = TimeSpan.FromSeconds(20);
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
    public async Task Prepare_timeout_above_the_npgsql_connect_timeout_cap_still_prepares()
    {
        var connectionString = await CreateDatabaseAsync();
        using var host = TestModulesHost.BuildUnstarted(
            connectionString,
            builder => builder.Configuration["App:Database:PrepareTimeout"] = "00:20:00");

        await Should.NotThrowAsync(
            () => host.Services.PrepareModuleDatabasesAsync(CancellationToken));
    }

    [Fact]
    public async Task Silent_module_connection_with_a_short_connect_timeout_fails_at_the_real_deadline()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var databaseProxy = DatabaseProxy.Start(connectionString);
        databaseProxy.AnswerOnlyNext(AdminConnectionCount);
        using var host = TestModulesHost.BuildUnstarted(
            WithConnectTimeout(databaseProxy.ConnectionString),
            builder =>
            {
                builder.Configuration["App:Database:PrepareTimeout"] = "00:00:10";
                builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
            });

        var stopwatch = Stopwatch.StartNew();
        var preparation = host.Services.PrepareModuleDatabasesAsync(CancellationToken);
        var exception = await Should.ThrowAsync<TimeoutException>(
            () => preparation.WaitAsync(RealTimeLimit, CancellationToken));

        exception.Message.ShouldStartWith(TimeoutMessage);
        stopwatch.Elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(9));
    }

    [Fact]
    public async Task Silent_server_during_the_lock_phase_ends_preparation_with_the_prepare_timeout_error()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var databaseProxy = DatabaseProxy.Start(connectionString);
        databaseProxy.AnswerOnlyNext(0);
        var fakeTimeProvider = new FakeTimeProvider(TestModulesHost.TestStartedOn);
        using var host = TestModulesHost.BuildUnstarted(databaseProxy.ConnectionString, fakeTimeProvider);
        var preparation = host.Services.PrepareModuleDatabasesAsync(CancellationToken);
        await WaitUntilHeldAsync(databaseProxy, connectionCount: 1, preparation);

        fakeTimeProvider.Advance(TestModulesHost.PrepareTimeout);
        var exception = await Should.ThrowAsync<TimeoutException>(
            () => preparation.WaitAsync(SilenceLimit, CancellationToken));

        exception.Message.ShouldStartWith(TimeoutMessage);
    }

    [Fact]
    public async Task Silent_module_connection_after_provisioning_fails_at_the_original_deadline()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var databaseProxy = DatabaseProxy.Start(connectionString);
        databaseProxy.AnswerOnlyNext(AdminConnectionCount);
        var fakeTimeProvider = new FakeTimeProvider(TestModulesHost.TestStartedOn);
        using var host = TestModulesHost.BuildUnstarted(databaseProxy.ConnectionString, fakeTimeProvider);
        var preparation = host.Services.PrepareModuleDatabasesAsync(CancellationToken);
        await WaitUntilHeldAsync(databaseProxy, connectionCount: 1, preparation);

        fakeTimeProvider.Advance(TestModulesHost.PrepareTimeout - TimeSpan.FromMinutes(1));
        await Task.Delay(SilenceLimit, CancellationToken);
        var isFinishedBeforeDeadline = preparation.IsCompleted;

        fakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        var exception = await Should.ThrowAsync<TimeoutException>(
            () => preparation.WaitAsync(SilenceLimit, CancellationToken));

        isFinishedBeforeDeadline.ShouldBeFalse();
        exception.Message.ShouldStartWith(TimeoutMessage);
    }

    [Fact]
    public async Task Cancellation_by_the_caller_during_a_silent_handshake_stays_a_cancellation()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var databaseProxy = DatabaseProxy.Start(connectionString);
        databaseProxy.AnswerOnlyNext(0);
        using var host = TestModulesHost.BuildUnstarted(databaseProxy.ConnectionString);
        using var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        var preparation = host.Services.PrepareModuleDatabasesAsync(callerCancellation.Token);
        await WaitUntilHeldAsync(databaseProxy, connectionCount: 1, preparation);

        await callerCancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => preparation.WaitAsync(SilenceLimit, CancellationToken));
    }

    [Fact]
    public async Task No_connection_stays_open_after_the_prepare_timeout()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var databaseProxy = DatabaseProxy.Start(connectionString);
        databaseProxy.AnswerOnlyNext(0);
        using var host = TestModulesHost.BuildUnstarted(
            WithConnectTimeout(databaseProxy.ConnectionString),
            builder =>
            {
                builder.Configuration["App:Database:PrepareTimeout"] = "00:00:10";
                builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
            });

        var preparation = host.Services.PrepareModuleDatabasesAsync(CancellationToken);
        await Should.ThrowAsync<TimeoutException>(() => preparation.WaitAsync(RealTimeLimit, CancellationToken));

        var stopwatch = Stopwatch.StartNew();
        while (databaseProxy.OpenSilentConnectionCount > 0 && stopwatch.Elapsed < ClosingLimit)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken);
        }

        databaseProxy.OpenSilentConnectionCount.ShouldBe(0);
    }

    [Fact]
    public async Task Unreachable_database_stops_preparation_at_once_naming_the_server()
    {
        var closedPort = GetClosedPort();
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = closedPort,
            Database = "monolith",
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
            builder => builder.Configuration["App:Modules:Pings:Limit"] = "0");

        await Should.ThrowAsync<OptionsValidationException>(
            () => host.Services.PrepareModuleDatabasesAsync(CancellationToken));

        var moduleSchemas = await new DatabaseProbe(connectionString).QueryAsync<String>(ModuleSchemasSql);

        moduleSchemas.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Only", "App:Database:PrepareMode is Only, which prepares the database and stops")]
    [InlineData("Never", "App:Database:PrepareMode is Never, so nothing may prepare the database")]
    public async Task Host_that_prepares_and_then_serves_refuses_a_mode_of_the_core_entry_point(
        String prepareMode,
        String refusal)
    {
        var connectionString = await CreateDatabaseAsync();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => TestModulesHost.StartAsync(
                connectionString,
                configure: builder => builder.Configuration["App:Database:PrepareMode"] = prepareMode));

        var moduleSchemas = await new DatabaseProbe(connectionString).QueryAsync<String>(ModuleSchemasSql);

        exception.Message.ShouldStartWith(refusal);
        exception.Message.ShouldContain(UpgradeNotes);
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

    private static async Task WaitUntilHeldAsync(DatabaseProxy databaseProxy, Int32 connectionCount, Task preparation)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!preparation.IsCompleted && databaseProxy.HeldConnectionCount < connectionCount)
        {
            stopwatch.Elapsed.ShouldBeLessThan(RealTimeLimit);
            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken);
        }

        preparation.IsCompleted.ShouldBeFalse();
    }

    private static async Task PrepareAsync(String connectionString)
    {
        using var host = TestModulesHost.BuildUnstarted(connectionString);
        await host.Services.PrepareModuleDatabasesAsync(CancellationToken);
    }

    private static String WithConnectTimeout(String connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = 2,
        }.ConnectionString;

    private static String BuildUnpooled(String connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        }.ConnectionString;

    private static Int32 GetClosedPort()
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
