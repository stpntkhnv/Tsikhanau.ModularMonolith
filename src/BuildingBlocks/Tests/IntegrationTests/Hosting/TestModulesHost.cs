using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ModularMonolith.BuildingBlocks.Api.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Cleanup;
using ModularMonolith.BuildingBlocks.Infrastructure.Database;
using ModularMonolith.BuildingBlocks.Infrastructure.Inbox;
using ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;
using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;
using ModularMonolith.BuildingBlocks.Infrastructure.Platform;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;
using ModularMonolith.Shared.Collections;
using ModularMonolith.Testing.Cleanup;
using ModularMonolith.Testing.Database;
using ModularMonolith.Testing.Queues;
using ModularMonolith.TestModules.Pings;
using ModularMonolith.TestModules.Pings.Contracts.Queries.GetPing;
using ModularMonolith.TestModules.Pings.Features;
using ModularMonolith.TestModules.Pings.Features.CreatePing;
using ModularMonolith.TestModules.Pings.Infrastructure.Persistence;
using ModularMonolith.TestModules.Pongs;
using ModularMonolith.TestModules.Pongs.Features;
using ModularMonolith.TestModules.Pongs.Features.CountPongs;
using ModularMonolith.TestModules.Pongs.Infrastructure.Persistence;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;

internal sealed class TestModulesHost : IAsyncDisposable
{
    public const Int32 ClaimBatchSize = 16;

    public static readonly DateTimeOffset TestStartedOn = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    public static readonly TimeSpan HandlerTimeout = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(10);

    private static readonly IReadOnlyDictionary<String, String> NoSettings = new Dictionary<String, String>();

    private readonly WebApplication _app;

    private TestModulesHost(WebApplication app, FakeTimeProvider fakeTimeProvider, String connectionString)
    {
        _app = app;
        Time = fakeTimeProvider;
        Database = new DatabaseProbe(connectionString);
    }

    public IServiceProvider Services => _app.Services;

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
        TimeSpan? handlerTimeout = null,
        Boolean writesLogs = true) =>
        StartAsync(
            postgresFixture.ConnectionString,
            runInBackground,
            handlerTimeout: handlerTimeout,
            writesLogs: writesLogs);

    public static Task<TestModulesHost> StartAsync(
        PostgresFixture postgresFixture,
        IReadOnlyDictionary<String, String> settings) =>
        BuildAndStartAsync(
            postgresFixture.ConnectionString,
            settings,
            runInBackground: false,
            maxConnections: 20,
            HandlerTimeout,
            writesLogs: true);

    public static Task<TestModulesHost> StartAsync(
        String connectionString,
        Boolean runInBackground = false,
        Int32 maxConnections = 20,
        TimeSpan? handlerTimeout = null,
        Boolean writesLogs = true) =>
        BuildAndStartAsync(
            connectionString,
            NoSettings,
            runInBackground,
            maxConnections,
            handlerTimeout ?? HandlerTimeout,
            writesLogs);

    internal static IHost BuildUnstarted(String connectionString) =>
        BuildUnstarted(connectionString, new FakeTimeProvider(TestStartedOn));

    internal static IHost BuildUnstarted(String connectionString, FakeTimeProvider fakeTimeProvider) =>
        Build(
            connectionString,
            NoSettings,
            fakeTimeProvider,
            runInBackground: false,
            maxConnections: 20,
            HandlerTimeout,
            writesLogs: true);

    internal static IHost BuildUnstarted(String connectionString, IReadOnlyDictionary<String, String> settings) =>
        Build(
            connectionString,
            settings,
            new FakeTimeProvider(TestStartedOn),
            runInBackground: false,
            maxConnections: 20,
            HandlerTimeout,
            writesLogs: true);

    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public HttpClient CreateClient() => _app.GetTestClient();

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
        await _app.StopAsync(CancellationToken.None);
        await _app.DisposeAsync();
    }

    private static async Task<TestModulesHost> BuildAndStartAsync(
        String connectionString,
        IReadOnlyDictionary<String, String> settings,
        Boolean runInBackground,
        Int32 maxConnections,
        TimeSpan handlerTimeout,
        Boolean writesLogs)
    {
        var fakeTimeProvider = new FakeTimeProvider(TestStartedOn);
        var app = Build(
            connectionString,
            settings,
            fakeTimeProvider,
            runInBackground,
            maxConnections,
            handlerTimeout,
            writesLogs);

        try
        {
            await TestModulesDatabase.PrepareAsync(app.Services, connectionString);
            await app.StartAsync(CancellationToken);
            return new TestModulesHost(app, fakeTimeProvider, connectionString);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    private static WebApplication Build(
        String connectionString,
        IReadOnlyDictionary<String, String> settings,
        FakeTimeProvider fakeTimeProvider,
        Boolean runInBackground,
        Int32 maxConnections,
        TimeSpan handlerTimeout,
        Boolean writesLogs)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Development });

        builder.WebHost.UseTestServer();
        if (!writesLogs)
        {
            builder.Logging.ClearProviders();
        }

        builder.Configuration[$"ConnectionStrings:{PlatformServiceCollectionExtensions.ConnectionName}"] =
            connectionString;

        builder.Configuration[$"{QueueSettings.Section}:{nameof(QueueSettings.RunInBackground)}"] =
            runInBackground.ToString();

        builder.Configuration[$"{QueueSettings.Section}:{nameof(QueueSettings.PollInterval)}"] = "00:00:05";
        builder.Configuration[$"{QueueSettings.Section}:{nameof(QueueSettings.HandlerTimeout)}"] =
            handlerTimeout.ToString("c", CultureInfo.InvariantCulture);

        builder.Configuration[$"{QueueSettings.Section}:{nameof(QueueSettings.ClaimBatchSize)}"] =
            ClaimBatchSize.ToString(CultureInfo.InvariantCulture);

        builder.Configuration[$"{CleanupSettings.Section}:{nameof(CleanupSettings.RunInBackground)}"] = "false";
        builder.Configuration[$"{CleanupSettings.Section}:{nameof(CleanupSettings.Interval)}"] = "01:00:00";
        builder.Configuration[$"{CleanupSettings.Section}:{nameof(CleanupSettings.KeepFor)}"] = "7.00:00:00";

        builder.Configuration[$"{DatabaseSettings.Section}:{nameof(DatabaseSettings.MaxConnections)}"] =
            maxConnections.ToString(CultureInfo.InvariantCulture);

        builder.Configuration[$"{DatabaseSettings.Section}:{nameof(DatabaseSettings.PrepareTimeout)}"] =
            PrepareTimeout.ToString("c", CultureInfo.InvariantCulture);

        builder.Configuration["ModularMonolith:Modules:Pings:Limit"] = "10";
        builder.Configuration["ModularMonolith:Modules:Pongs:Limit"] = "10";
        settings.ForEach(setting => builder.Configuration[setting.Key] = setting.Value);

        builder.EnsureConnectionString();
        builder.Services.AddPlatformServices(builder.Configuration);
        builder.Services.AddSingleton<TimeProvider>(fakeTimeProvider);
        builder.AddModule<PingsModule>();
        builder.AddModule<PongsModule>();

        var app = builder.Build();
        app.UseModules();
        app.MapModules();
        return app;
    }

    private TPump ResolvePump<TPump>()
        where TPump : notnull =>
        Services.GetRequiredService<TPump>();
}
