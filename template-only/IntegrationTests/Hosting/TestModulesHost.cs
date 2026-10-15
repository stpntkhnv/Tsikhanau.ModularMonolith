using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Tsikhanau.Monolith.Api.Callers;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Hosting;
using Tsikhanau.Monolith.Infrastructure.Cleanup;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Infrastructure.Platform;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.Testing.App;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.Testing.Queues;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;
using Tsikhanau.Monolith.TestModules.Pings.Features;
using Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Monolith.TestModules.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Features;
using Tsikhanau.Monolith.TestModules.Pongs.Features.CountPongs;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class TestModulesHost : IAsyncDisposable
{
    public const Int32 ClaimBatchSize = 16;

    public static readonly DateTimeOffset TestStartedOn = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    public static readonly TimeSpan HandlerTimeout = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(10);

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

    public JsonSerializerOptions HttpJsonSerializerOptions =>
        Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static Task<TestModulesHost> StartAsync(
        PostgresFixture postgresFixture,
        Boolean runInBackground = false,
        TimeSpan? handlerTimeout = null,
        Boolean writesLogs = true,
        Action<IHostApplicationBuilder>? configure = null,
        Boolean requiresAuthentication = false) =>
        StartAsync(
            postgresFixture.ConnectionString,
            runInBackground,
            handlerTimeout: handlerTimeout,
            writesLogs: writesLogs,
            configure: configure,
            requiresAuthentication: requiresAuthentication);

    public static Task<TestModulesHost> StartAsync(
        String connectionString,
        Boolean runInBackground = false,
        Int32 maxConnections = 20,
        TimeSpan? handlerTimeout = null,
        Boolean writesLogs = true,
        Action<IHostApplicationBuilder>? configure = null,
        String? environmentName = null,
        Boolean requiresAuthentication = false) =>
        BuildAndStartAsync(
            connectionString,
            runInBackground,
            maxConnections,
            handlerTimeout ?? HandlerTimeout,
            writesLogs,
            configure,
            environmentName ?? Environments.Development,
            requiresAuthentication);

    internal static IHost BuildUnstarted(String connectionString) =>
        BuildUnstarted(connectionString, new FakeTimeProvider(TestStartedOn));

    internal static IHost BuildUnstarted(String connectionString, FakeTimeProvider fakeTimeProvider) =>
        Build(
            connectionString,
            fakeTimeProvider,
            runInBackground: false,
            maxConnections: 20,
            HandlerTimeout,
            writesLogs: true,
            configure: null,
            Environments.Development,
            requiresAuthentication: false);

    internal static IHost BuildUnstarted(String connectionString, Action<IHostApplicationBuilder> configure) =>
        Build(
            connectionString,
            new FakeTimeProvider(TestStartedOn),
            runInBackground: false,
            maxConnections: 20,
            HandlerTimeout,
            writesLogs: true,
            configure,
            Environments.Development,
            requiresAuthentication: false);

    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public HttpClient CreateClient() => _app.GetTestClient();

    public async Task<Guid> CreatePingAsync(String label)
    {
        var pingId = Guid.CreateVersion7();
        var result = await Pings.ExecuteCommandAsync(
            new CreatePingCommand(pingId, label, Optional<String>.None()),
            CancellationToken);

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

    public void ClearModulePools() =>
        Services
            .GetRequiredService<ModuleRegistry>().Modules
            .Select(module => Services.GetRequiredKeyedService<NpgsqlDataSource>(module.Key))
            .ForEach(npgsqlDataSource => npgsqlDataSource.Clear());

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
        Boolean runInBackground,
        Int32 maxConnections,
        TimeSpan handlerTimeout,
        Boolean writesLogs,
        Action<IHostApplicationBuilder>? configure,
        String environmentName,
        Boolean requiresAuthentication)
    {
        var fakeTimeProvider = new FakeTimeProvider(TestStartedOn);
        var app = Build(
            connectionString,
            fakeTimeProvider,
            runInBackground,
            maxConnections,
            handlerTimeout,
            writesLogs,
            configure,
            environmentName,
            requiresAuthentication);

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
        FakeTimeProvider fakeTimeProvider,
        Boolean runInBackground,
        Int32 maxConnections,
        TimeSpan handlerTimeout,
        Boolean writesLogs,
        Action<IHostApplicationBuilder>? configure,
        String environmentName,
        Boolean requiresAuthentication)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });

        builder.WebHost.UseTestServer();
        builder.AddHttpDefaults();
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

        builder.Configuration[$"{DatabaseSettings.Section}:{nameof(DatabaseSettings.LoginKey)}"] =
            TestSettings.LoginKey;

        builder.Configuration[$"{DatabaseSettings.Section}:{nameof(DatabaseSettings.MaxConnections)}"] =
            maxConnections.ToString(CultureInfo.InvariantCulture);

        builder.Configuration[$"{DatabaseSettings.Section}:{nameof(DatabaseSettings.PrepareTimeout)}"] =
            PrepareTimeout.ToString("c", CultureInfo.InvariantCulture);

        builder.Configuration["App:Modules:Pings:Limit"] = "10";
        builder.Configuration["App:Modules:Pongs:Limit"] = "10";

        builder.EnsureConnectionString();
        builder.Services.AddPlatformServices(builder.Configuration);
        builder.Services.AddSingleton<TimeProvider>(fakeTimeProvider);
        builder.AddModule<PingsModule>();
        builder.AddModule<PongsModule>();
        if (requiresAuthentication)
        {
            RequireCallers(builder.Services);
        }

        configure?.Invoke(builder);

        var app = builder.Build();
        app.UseServiceDefaults();
        app.UseModules();
        app.MapModules();
        return app;
    }

    private static void RequireCallers(IServiceCollection services)
    {
        services
            .AddAuthentication(TestCallerAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestCallerAuthenticationHandler>(
                TestCallerAuthenticationHandler.SchemeName,
                configureOptions: null);

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(
                new AuthorizationPolicyBuilder()
                    .RequireCaller()
                    .Build());
    }

    private TPump ResolvePump<TPump>()
        where TPump : notnull =>
        Services.GetRequiredService<TPump>();
}
