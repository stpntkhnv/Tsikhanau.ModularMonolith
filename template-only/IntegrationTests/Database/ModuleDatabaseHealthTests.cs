using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Shared.Collections;

namespace Tsikhanau.Monolith.IntegrationTests.Database;

public sealed class ModuleDatabaseHealthTests(PostgresFixture postgresFixture)
{
    private static readonly TimeSpan HealthAnswerLimit = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan RecoveryLimit = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan RecoveryPollInterval = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan OverlapDeadline = TimeSpan.FromSeconds(10);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_turns_unhealthy_within_five_seconds_when_a_pooled_connection_stops_answering()
    {
        await using var databaseProxy = DatabaseProxy.Start(postgresFixture.ConnectionString);
        await using var host = await TestModulesHost.StartAsync(databaseProxy.ConnectionString);
        var healthyCheck = await CheckHealthAsync(host);

        databaseProxy.StopAnswering();
        var unansweredCheck = await CheckHealthAsync(host);

        healthyCheck.Status.ShouldBe(HealthStatus.Healthy);
        unansweredCheck.Status.ShouldBe(HealthStatus.Unhealthy);
        unansweredCheck.Elapsed.ShouldBeLessThan(HealthAnswerLimit);
    }

    [Fact]
    public async Task Health_checks_during_an_outage_answer_in_time_and_open_one_connection_per_module()
    {
        await using var databaseProxy = DatabaseProxy.Start(postgresFixture.ConnectionString);
        await using var host = await TestModulesHost.StartAsync(databaseProxy.ConnectionString);
        var moduleCount = host.Services.GetRequiredService<ModuleRegistry>().Modules.Count;

        host.ClearModulePools();
        databaseProxy.StopAnswering();
        var unansweredChecks = new List<HealthCheckOutcome>();
        for (var checkNumber = 0; checkNumber < 3; checkNumber++)
        {
            unansweredChecks.Add(await CheckHealthAsync(host));
        }

        unansweredChecks.ShouldAllBe(check => check.Status == HealthStatus.Unhealthy);
        unansweredChecks.ShouldAllBe(check => check.Elapsed < HealthAnswerLimit);
        databaseProxy.HeldConnectionCount.ShouldBe(moduleCount);
    }

    [Fact]
    public async Task Health_recovers_within_ten_seconds_once_the_database_answers_again()
    {
        await using var databaseProxy = DatabaseProxy.Start(postgresFixture.ConnectionString);
        await using var host = await TestModulesHost.StartAsync(databaseProxy.ConnectionString);
        databaseProxy.StopAnswering();
        var unansweredCheck = await CheckHealthAsync(host);

        databaseProxy.StartAnswering();
        var recovery = await MeasureRecoveryAsync(host);

        unansweredCheck.Status.ShouldBe(HealthStatus.Unhealthy);
        recovery.ShouldBeLessThan(RecoveryLimit);
    }

    [Fact]
    public async Task Overlapping_checks_join_the_probe_in_flight_and_both_answer_healthy()
    {
        await using var databaseProxy = DatabaseProxy.Start(postgresFixture.ConnectionString);
        await using var host = await TestModulesHost.StartAsync(databaseProxy.ConnectionString);
        var healthCheck = ResolveHealthCheckOfTheFirstModule(host);
        var healthCheckContext = new HealthCheckContext();

        host.ClearModulePools();
        databaseProxy.StopAnswering();
        var firstCheck = healthCheck.CheckHealthAsync(healthCheckContext, CancellationToken);
        await WaitUntilHeldAsync(databaseProxy, connectionCount: 1);
        var overlappingCheck = healthCheck.CheckHealthAsync(healthCheckContext, CancellationToken);
        var overlappingCheckWaits = !overlappingCheck.IsCompleted;
        var heldConnectionsBeforeRelease = databaseProxy.HeldConnectionCount;

        databaseProxy.StartAnswering();
        HealthCheckResult[] results =
        [
            await firstCheck.WaitAsync(OverlapDeadline, CancellationToken),
            await overlappingCheck.WaitAsync(OverlapDeadline, CancellationToken),
        ];

        overlappingCheckWaits.ShouldBeTrue();
        heldConnectionsBeforeRelease.ShouldBe(1);
        results.ShouldAllBe(result => result.Status == HealthStatus.Healthy);
    }

    private static async Task<HealthCheckOutcome> CheckHealthAsync(TestModulesHost host)
    {
        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var startedOn = Stopwatch.GetTimestamp();

        var healthReport = await healthCheckService.CheckHealthAsync(CancellationToken);

        return new HealthCheckOutcome(healthReport.Status, Stopwatch.GetElapsedTime(startedOn));
    }

    private static async Task<TimeSpan> MeasureRecoveryAsync(TestModulesHost host)
    {
        var startedOn = Stopwatch.GetTimestamp();
        var check = await CheckHealthAsync(host);
        while (check.Status != HealthStatus.Healthy && Stopwatch.GetElapsedTime(startedOn) < RecoveryLimit)
        {
            await Task.Delay(RecoveryPollInterval, CancellationToken);
            check = await CheckHealthAsync(host);
        }

        return Stopwatch.GetElapsedTime(startedOn);
    }

    private static async Task WaitUntilHeldAsync(DatabaseProxy databaseProxy, Int32 connectionCount)
    {
        var stopwatch = Stopwatch.StartNew();
        while (databaseProxy.HeldConnectionCount < connectionCount)
        {
            stopwatch.Elapsed.ShouldBeLessThan(HealthAnswerLimit);
            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken);
        }
    }

    private static ModuleDatabaseHealthCheck ResolveHealthCheckOfTheFirstModule(TestModulesHost host)
    {
        var module = host.Services
            .GetRequiredService<ModuleRegistry>().Modules
            .First();

        return host.Services.GetRequiredKeyedService<ModuleDatabaseHealthCheck>(module.Key);
    }

    private sealed record HealthCheckOutcome(HealthStatus Status, TimeSpan Elapsed);

    private sealed class DatabaseProxy : IAsyncDisposable
    {
        private const Int32 BufferSize = 8192;

        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stoppingSource = new();
        private readonly ConcurrentBag<TcpClient> _connections = [];
        private readonly ConcurrentBag<Task> _forwardings = [];
        private readonly Lock _answeringLock = new();
        private readonly List<TcpClient> _heldClients = [];
        private readonly String _serverHost;
        private readonly Int32 _serverPort;
        private readonly Task _accepting;
        private volatile Boolean _isAnswering = true;
        private Int32 _heldConnectionCount;

        private DatabaseProxy(NpgsqlConnectionStringBuilder serverConnectionStringBuilder)
        {
            _serverHost = serverConnectionStringBuilder.Host!;
            _serverPort = serverConnectionStringBuilder.Port;
            _listener.Start();
            ConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionStringBuilder.ConnectionString)
            {
                Host = IPAddress.Loopback.ToString(),
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port,
            }.ConnectionString;

            _accepting = AcceptAsync();
        }

        public String ConnectionString { get; }

        public Int32 HeldConnectionCount => Volatile.Read(ref _heldConnectionCount);

        public static DatabaseProxy Start(String serverConnectionString) =>
            new(new NpgsqlConnectionStringBuilder(serverConnectionString));

        public void StopAnswering()
        {
            lock (_answeringLock)
            {
                _isAnswering = false;
            }
        }

        public void StartAnswering()
        {
            lock (_answeringLock)
            {
                _isAnswering = true;
                _heldClients.ForEach(Forward);
                _heldClients.Clear();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stoppingSource.CancelAsync();
            _listener.Stop();
            _connections.ForEach(connection => connection.Dispose());
            await _accepting;
            await Task.WhenAll(_forwardings);
            _stoppingSource.Dispose();
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync(_stoppingSource.Token);
                    _connections.Add(client);
                    Admit(client);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void Admit(TcpClient client)
        {
            lock (_answeringLock)
            {
                if (_isAnswering)
                {
                    Forward(client);
                    return;
                }

                _heldClients.Add(client);
                Interlocked.Increment(ref _heldConnectionCount);
            }
        }

        private void Forward(TcpClient client) => _forwardings.Add(ForwardAsync(client));

        private async Task ForwardAsync(TcpClient client)
        {
            var server = new TcpClient();
            _connections.Add(server);
            try
            {
                await server.ConnectAsync(_serverHost, _serverPort, _stoppingSource.Token);
                await Task.WhenAll(
                    CopyAsync(client.GetStream(), server.GetStream()),
                    CopyAsync(server.GetStream(), client.GetStream()));
            }
            catch (Exception exception) when (IsConnectionEnd(exception))
            {
            }
        }

        private async Task CopyAsync(NetworkStream source, NetworkStream destination)
        {
            var buffer = new Byte[BufferSize];
            var readCount = await source.ReadAsync(buffer, _stoppingSource.Token);
            while (readCount > 0)
            {
                if (_isAnswering)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, readCount), _stoppingSource.Token);
                }

                readCount = await source.ReadAsync(buffer, _stoppingSource.Token);
            }

            destination.Socket.Shutdown(SocketShutdown.Send);
        }

        private static Boolean IsConnectionEnd(Exception exception) =>
            exception is IOException or SocketException or ObjectDisposedException or OperationCanceledException;
    }
}
