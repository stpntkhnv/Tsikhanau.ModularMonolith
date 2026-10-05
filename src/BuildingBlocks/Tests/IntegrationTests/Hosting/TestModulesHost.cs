using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using ModularMonolith.BuildingBlocks.Infrastructure.Cleanup;
using ModularMonolith.BuildingBlocks.Infrastructure.Database;
using ModularMonolith.BuildingBlocks.Infrastructure.Inbox;
using ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;
using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;
using ModularMonolith.BuildingBlocks.Infrastructure.Platform;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;
using ModularMonolith.Testing.Cleanup;
using ModularMonolith.Testing.Database;
using ModularMonolith.Testing.Queues;
using ModularMonolith.TestModules.Pings;
using ModularMonolith.TestModules.Pings.Contracts.Queries.GetPing;
using ModularMonolith.TestModules.Pings.Features;
using ModularMonolith.TestModules.Pings.Features.CreatePing;
using ModularMonolith.TestModules.Pings.Infrastructure;
using ModularMonolith.TestModules.Pings.Infrastructure.Persistence;
using ModularMonolith.TestModules.Pongs;
using ModularMonolith.TestModules.Pongs.Features;
using ModularMonolith.TestModules.Pongs.Features.CountPongs;
using ModularMonolith.TestModules.Pongs.Infrastructure;
using ModularMonolith.TestModules.Pongs.Infrastructure.Persistence;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;

internal sealed class TestModulesHost : IAsyncDisposable
{
    public static readonly DateTimeOffset TestStartedOn = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    public static readonly TimeSpan HandlerTimeout = TimeSpan.FromMinutes(1);

    private readonly IHost _host;

    private TestModulesHost(IHost host, FakeTimeProvider fakeTimeProvider, String connectionString)
    {
        _host = host;
        Time = fakeTimeProvider;
        Database = new DatabaseProbe(connectionString);
    }

    public IServiceProvider Services => _host.Services;

    public FakeTimeProvider Time { get; }

    public DatabaseProbe Database { get; }

    public IPingsModule Pings => Services.GetRequiredService<IPingsModule>();

    public IPongsModule Pongs => Services.GetRequiredService<IPongsModule>();

    public PingTestHooks PingHooks => Services.GetRequiredService<PingTestHooks>();

    public PongTestHooks PongHooks => Services.GetRequiredService<PongTestHooks>();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static Task<TestModulesHost> StartAsync(
        PostgresFixture postgresFixture,
        Boolean runInBackground = false,
        TimeSpan? handlerTimeout = null) =>
        StartAsync(postgresFixture.ConnectionString, runInBackground, handlerTimeout: handlerTimeout);

    public static async Task<TestModulesHost> StartAsync(
        String connectionString,
        Boolean runInBackground = false,
        Int32 maxConnections = 20,
        TimeSpan? handlerTimeout = null)
    {
        var fakeTimeProvider = new FakeTimeProvider(TestStartedOn);
        var host = Build(
            connectionString,
            fakeTimeProvider,
            runInBackground,
            maxConnections,
            handlerTimeout ?? HandlerTimeout);

        try
        {
            await TestModulesDatabase.PrepareAsync(host.Services, connectionString);
            await host.StartAsync(CancellationToken);
            return new TestModulesHost(host, fakeTimeProvider, connectionString);
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    internal static IHost BuildUnstarted(String connectionString) =>
        Build(
            connectionString,
            new FakeTimeProvider(TestStartedOn),
            runInBackground: false,
            maxConnections: 20,
            HandlerTimeout);

    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public async Task<Guid> CreatePingAsync(String label)
    {
        var pingId = Guid.CreateVersion7();
        var result = await Pings.ExecuteCommandAsync(new CreatePingCommand(pingId, label), CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        return pingId;
    }

    public async Task<PingDto> GetPingAsync(Guid pingId)
    {
        var result = await Pings.ExecuteQueryAsync(new GetPingQuery(pingId), CancellationToken);

        return result.Value;
    }

    public async Task<Int32> CountPongsAsync()
    {
        var result = await Pongs.ExecuteQueryAsync(new CountPongsQuery(), CancellationToken);

        return result.Value;
    }

    public Task<Int32> DeliverOutboxAsync() =>
        ResolvePump<OutboxPump<IPingsModule>>()
            .RunOnceAsync(CancellationToken);

    public Task<Int32> ProcessInboxAsync(CancellationToken cancellationToken) =>
        ResolvePump<InboxPump<IPongsModule>>()
            .RunOnceAsync(cancellationToken);

    public Task<Int32> RunInternalCommandsAsync() =>
        ResolvePump<InternalCommandsPump<IPingsModule>>()
            .RunOnceAsync(CancellationToken);

    public Task<Int32> RunPingsQueuesAsync() =>
        ResolvePump<IQueuePump<IPingsModule>>()
            .RunOnceAsync(CancellationToken);

    public Task<Int32> RunCleanupAsync() => CleanupDriver.RunAllAsync(Services);

    public Task ProcessQueuesAsync() => QueueDriver.ProcessAllAsync(Services);

    public Task ProcessQueuesAllowingFailuresAsync() => QueueDriver.ProcessAllAllowingFailuresAsync(Services);

    public Task<IReadOnlyList<QueueRow>> ReadOutboxRowsAsync() =>
        Database.ReadQueueRowsAsync(PingsDbContext.SchemaName, OutboxMessage.Table);

    public Task<IReadOnlyList<QueueRow>> ReadInboxRowsAsync() =>
        Database.ReadQueueRowsAsync(PongsDbContext.SchemaName, InboxMessage.Table);

    public Task<IReadOnlyList<QueueRow>> ReadInternalCommandRowsAsync() =>
        Database.ReadQueueRowsAsync(PingsDbContext.SchemaName, InternalCommandMessage.Table);

    public void SetLoadBarrier(Int32 loadCount)
    {
        var arrivedCount = 0;
        var allLoadedCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PingHooks.AfterLoad = () =>
        {
            if (Interlocked.Increment(ref arrivedCount) == loadCount)
            {
                allLoadedCompletionSource.TrySetResult();
            }

            return allLoadedCompletionSource.Task;
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(CancellationToken.None);
        _host.Dispose();
    }

    private static IHost Build(
        String connectionString,
        FakeTimeProvider fakeTimeProvider,
        Boolean runInBackground,
        Int32 maxConnections,
        TimeSpan handlerTimeout)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });

        builder.Configuration[$"ConnectionStrings:{PlatformServiceCollectionExtensions.ConnectionName}"] =
            connectionString;

        builder.Configuration[$"{QueueSettings.Section}:{nameof(QueueSettings.RunInBackground)}"] =
            runInBackground.ToString();

        builder.Configuration[$"{QueueSettings.Section}:{nameof(QueueSettings.PollInterval)}"] = "00:00:05";
        builder.Configuration[$"{QueueSettings.Section}:{nameof(QueueSettings.HandlerTimeout)}"] =
            handlerTimeout.ToString("c", CultureInfo.InvariantCulture);

        builder.Configuration[$"{CleanupSettings.Section}:{nameof(CleanupSettings.RunInBackground)}"] = "false";
        builder.Configuration[$"{CleanupSettings.Section}:{nameof(CleanupSettings.Interval)}"] = "01:00:00";
        builder.Configuration[$"{CleanupSettings.Section}:{nameof(CleanupSettings.KeepFor)}"] = "7.00:00:00";

        builder.Configuration[$"{DatabaseSettings.Section}:{nameof(DatabaseSettings.MaxConnections)}"] =
            maxConnections.ToString(CultureInfo.InvariantCulture);

        builder.EnsureConnectionString();
        builder.Services.AddPlatformServices(builder.Configuration);
        builder.Services.AddSingleton<TimeProvider>(fakeTimeProvider);
        builder.Services.AddPingsInfrastructure();
        builder.Services.AddPongsInfrastructure();

        return builder.Build();
    }

    private TPump ResolvePump<TPump>()
        where TPump : notnull =>
        Services.GetRequiredService<TPump>();
}
