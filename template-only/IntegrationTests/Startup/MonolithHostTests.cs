using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Hosting;
using Tsikhanau.Monolith.Infrastructure.Cleanup;
using Tsikhanau.Monolith.Infrastructure.Database.Settings;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;
using Tsikhanau.Monolith.Infrastructure.Platform;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.IntegrationTests.Hosts.OutOfProcess;
using Tsikhanau.Monolith.IntegrationTests.Postgres;
using Tsikhanau.Monolith.Testing.App;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pongs;

namespace Tsikhanau.Monolith.IntegrationTests.Startup;

public sealed class MonolithHostTests(PostgresFixture postgresFixture)
{
    private const Int32 Done = 0;

    private const Int32 Failed = 1;

    private const Int32 UsageError = 2;

    private const String OnStartup = "OnStartup";

    private const String Only = "Only";

    private const String Never = "Never";

    private const String AnyFreePort = "http://127.0.0.1:0";

    private const String ClosedOtlpEndpoint = "http://127.0.0.1:1";

    private const String OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    private const String ConnectionStringKey = "ConnectionStrings:database";

    private const String SelfCheckName = "self";

    private const String SslModeRequirement = "must set SSL Mode";

    private const String MessageId = "01999a00-0000-7000-8000-000000000001";

    private const String ConnectionStringWithoutSslMode =
        "Host=127.0.0.1;Port=1;Database=startup;Username=postgres;Password=postgres";

    private const String ConnectionStringWithoutCredentials = "Host=127.0.0.1;Port=1;Database=startup;SSL Mode=Disable";

    private const String InsertFailedRowSql = """
        INSERT INTO pongs.inbox_messages
            (id, type, payload, attempts, next_attempt_on, last_error, failed_on, occurred_on, received_on)
        VALUES (@Id::uuid, 'pings.ping-created', '{"label":"broken"}', 1, @FailedOn, @LastError, @FailedOn,
                @FailedOn, @FailedOn)
        """;

    private const String ModuleRolesSql = "SELECT rolname FROM pg_roles WHERE starts_with(rolname, @Prefix)";

    private static readonly DateTimeOffset FailedOn = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(2);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Program_of_core_102_registers_every_core_step_once_and_the_host_serves_health()
    {
        var connectionString = await CreateDatabaseAsync();
        using var environmentOverride = new EnvironmentOverride(
            CreateEnvironment(connectionString, OnStartup, ClosedOtlpEndpoint));

        var hostStarted = CreateHostStartedSource();

        var hostRun = MonolithHost.RunAsync([], RecordingHostStart(AddServicesOfCore102Program, hostStarted));
        var serviceProvider = await WaitUntilStartedAsync(hostRun, hostStarted.Task);
        var registrationCounts = DescribeRegistrationCounts(serviceProvider);
        var isCoreMeterExported = IsCoreMeterExported(serviceProvider);
        var health = await ReadHealthAsync(serviceProvider);
        StopApplication(serviceProvider);
        var exitCode = await hostRun;

        registrationCounts.ShouldBe(
        [
            "self 1",
            "QueueSettings 1",
            "CleanupSettings 1",
            "DatabaseSettings 1",
            "DatabaseConnectionStrings 1",
        ]);

        isCoreMeterExported.ShouldBeTrue();
        health.ShouldBe("200 Healthy");
        exitCode.ShouldBe(Done);
    }

    [Fact]
    public async Task Queues_command_with_the_program_of_core_102_retries_a_failed_message_without_starting_the_host()
    {
        var connectionString = await CreatePreparedDatabaseAsync();
        await InsertFailedRowAsync(connectionString);
        using var environmentOverride = new EnvironmentOverride(CreateEnvironment(connectionString, OnStartup));
        var hostStarted = CreateHostStartedSource();

        var exitCode = await MonolithHost.RunAsync(
            ["queues", "retry", "Pongs", "inbox_messages", MessageId],
            RecordingHostStart(AddServicesOfCore102Program, hostStarted));

        var retriedRow = await ReadInboxRowAsync(connectionString);

        exitCode.ShouldBe(Done);
        retriedRow.Attempts.ShouldBe(0);
        retriedRow.HasFailed.ShouldBeFalse();
        hostStarted.Task.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Queues_command_with_the_program_of_core_102_prepares_no_database()
    {
        var connectionString = await CreateDatabaseAsync();
        using var environmentOverride = new EnvironmentOverride(CreateEnvironment(connectionString, OnStartup));

        var exitCode = await MonolithHost.RunAsync(
            ["queues", "retry", "Pongs", "inbox_messages", MessageId],
            AddServicesOfCore102Program);

        var roles = await ReadModuleRolesAsync(connectionString);

        exitCode.ShouldBe(Failed);
        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task Queues_command_with_the_program_of_core_102_keeps_its_arguments_out_of_the_configuration()
    {
        var connectionString = BuildWithoutSslRequirement(postgresFixture.ConnectionString);
        using var environmentOverride = new EnvironmentOverride(CreateEnvironment(connectionString, OnStartup));

        var exitCode = await MonolithHost.RunAsync(
            ["queues", "list", $"--{ConnectionStringKey}="],
            AddServicesOfCore102Program);

        exitCode.ShouldBe(UsageError);
    }

    [Fact]
    public async Task Connection_string_of_a_source_added_in_add_services_is_checked_before_the_host_is_built()
    {
        using var environmentOverride = new EnvironmentOverride(CreateEnvironment(String.Empty, OnStartup));
        var settingsValidated = new TaskCompletionSource();
        var addServices = AddingSource(new() { [ConnectionStringKey] = ConnectionStringWithoutSslMode });

        var startupFailure = await Should.ThrowAsync<InvalidOperationException>(
            () => MonolithHost.RunAsync([], RecordingSettingsValidation(addServices, settingsValidated)));

        startupFailure.Message.ShouldContain(SslModeRequirement);
        settingsValidated.Task.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Connection_string_and_otlp_endpoint_of_a_source_added_in_add_services_start_the_exporting_host()
    {
        using var environmentOverride = new EnvironmentOverride(CreateEnvironment(String.Empty, Never));
        var hostStarted = CreateHostStartedSource();
        var addServices = AddingSource(
            new()
            {
                [ConnectionStringKey] = ConnectionStringWithoutCredentials,
                [OtlpEndpointKey] = ClosedOtlpEndpoint,
            });

        var hostRun = MonolithHost.RunAsync([], RecordingHostStart(addServices, hostStarted));
        var serviceProvider = await WaitUntilStartedAsync(hostRun, hostStarted.Task);
        var isCoreMeterExported = IsCoreMeterExported(serviceProvider);
        StopApplication(serviceProvider);
        var exitCode = await hostRun;

        isCoreMeterExported.ShouldBeTrue();
        exitCode.ShouldBe(Done);
    }

    [Fact]
    public async Task Logging_providers_cleared_in_add_services_stay_cleared_when_the_otlp_endpoint_is_set()
    {
        using var environmentOverride = new EnvironmentOverride(
            CreateEnvironment(ConnectionStringWithoutCredentials, Never, ClosedOtlpEndpoint));

        var hostStarted = CreateHostStartedSource();

        var hostRun = MonolithHost.RunAsync([], RecordingHostStart(ClearLoggingProviders, hostStarted));
        var serviceProvider = await WaitUntilStartedAsync(hostRun, hostStarted.Task);
        var loggerProviderNames = ReadLoggerProviderNames(serviceProvider);
        var isCoreMeterExported = IsCoreMeterExported(serviceProvider);
        StopApplication(serviceProvider);
        var exitCode = await hostRun;

        loggerProviderNames.ShouldBeEmpty();
        isCoreMeterExported.ShouldBeTrue();
        exitCode.ShouldBe(Done);
    }

    private static void ClearLoggingProviders(WebApplicationBuilder builder) =>
        builder.Logging.ClearProviders();

    private static void AddServicesOfCore102Program(WebApplicationBuilder builder)
    {
        builder.AddServiceDefaults(AppDiagnostics.SourceName);
        builder.EnsureConnectionString();
        builder.Services.AddPlatformServices(builder.Configuration);
        builder.AddModule<PingsModule>();
        builder.AddModule<PongsModule>();
    }

    private static Action<WebApplicationBuilder> AddingSource(Dictionary<String, String?> settings) =>
        builder => builder.Configuration.AddInMemoryCollection(settings);

    private static Action<WebApplicationBuilder> RecordingHostStart(
        Action<WebApplicationBuilder> addServices,
        TaskCompletionSource<IServiceProvider> hostStarted) =>
        builder =>
        {
            addServices(builder);
            builder.Services.AddHostedService(serviceProvider => new HostStartProbe(serviceProvider, hostStarted));
        };

    private static Action<WebApplicationBuilder> RecordingSettingsValidation(
        Action<WebApplicationBuilder> addServices,
        TaskCompletionSource settingsValidated) =>
        builder =>
        {
            addServices(builder);
            builder.Services
                .AddOptions<SettingsValidationProbe>()
                .Validate(_ => settingsValidated.TrySetResult())
                .ValidateOnStart();
        };

    private static TaskCompletionSource<IServiceProvider> CreateHostStartedSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<IServiceProvider> WaitUntilStartedAsync(
        Task<Int32> hostRun,
        Task<IServiceProvider> hostStarted)
    {
        var finishedFirst = await Task
            .WhenAny(hostStarted, hostRun)
            .WaitAsync(StartTimeout, CancellationToken);

        if (finishedFirst == hostRun)
        {
            var exitCode = await hostRun;
            throw new InvalidOperationException($"The host exited with {exitCode} before it started");
        }

        return await hostStarted;
    }

    private static void StopApplication(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<IHostApplicationLifetime>()
            .StopApplication();

    private static IReadOnlyList<String> DescribeRegistrationCounts(IServiceProvider serviceProvider) =>
    [
        $"{SelfCheckName} {CountSelfChecks(serviceProvider)}",
        DescribeValidatorCount<QueueSettings>(serviceProvider),
        DescribeValidatorCount<CleanupSettings>(serviceProvider),
        DescribeValidatorCount<DatabaseSettings>(serviceProvider),
        DescribeServiceCount<DatabaseConnectionStrings>(serviceProvider),
    ];

    private static Int32 CountSelfChecks(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Count(registration => registration.Name == SelfCheckName);

    private static String DescribeValidatorCount<TSettings>(IServiceProvider serviceProvider)
        where TSettings : class =>
        $"{typeof(TSettings).Name} {CountServices<IValidateOptions<TSettings>>(serviceProvider)}";

    private static String DescribeServiceCount<TService>(IServiceProvider serviceProvider) =>
        $"{typeof(TService).Name} {CountServices<TService>(serviceProvider)}";

    private static Int32 CountServices<TService>(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetServices<TService>()
            .Count();

    private static IReadOnlyList<String> ReadLoggerProviderNames(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetServices<ILoggerProvider>()
            .Select(loggerProvider => loggerProvider.GetType().Name)
            .ToList();

    private static Boolean IsCoreMeterExported(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<IMeterFactory>()
            .Create(AppDiagnostics.SourceName)
            .CreateCounter<Int64>("tsikhanau.startup.probe")
            .Enabled;

    private static async Task<String> ReadHealthAsync(IServiceProvider serviceProvider)
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(ReadServerAddress(serviceProvider)) };
        using var response = await httpClient.GetAsync("/health", CancellationToken);
        var body = await response.Content.ReadAsStringAsync(CancellationToken);

        return $"{(Int32)response.StatusCode} {body}";
    }

    private static String ReadServerAddress(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<IServer>().Features
            .GetRequiredFeature<IServerAddressesFeature>().Addresses
            .Single();

    private static Dictionary<String, String> CreateEnvironment(String connectionString, String prepareMode) =>
        CreateEnvironment(connectionString, prepareMode, String.Empty);

    private static Dictionary<String, String> CreateEnvironment(
        String connectionString,
        String prepareMode,
        String otlpEndpoint) =>
        new()
        {
            ["ASPNETCORE_ENVIRONMENT"] = Environments.Production,
            ["DOTNET_ENVIRONMENT"] = Environments.Production,
            [TestHostEnvironment.UrlsKey] = AnyFreePort,
            ["Logging__LogLevel__Default"] = "Warning",
            [OtlpEndpointKey] = otlpEndpoint,
            ["ConnectionStrings__database"] = connectionString,
            ["App__Database__LoginKey"] = TestSettings.LoginKey,
            ["App__Database__PrepareMode"] = prepareMode,
            ["App__Database__MaxConnections"] = "20",
            ["App__Database__PrepareTimeout"] = "00:10:00",
            ["App__Queues__RunInBackground"] = "false",
            ["App__Queues__PollInterval"] = "00:00:05",
            ["App__Queues__HandlerTimeout"] = "00:01:00",
            ["App__Queues__ClaimBatchSize"] = "16",
            ["App__Cleanup__RunInBackground"] = "false",
            ["App__Cleanup__Interval"] = "01:00:00",
            ["App__Cleanup__KeepFor"] = "7.00:00:00",
            ["App__Modules__Pings__Limit"] = "10",
            ["App__Modules__Pongs__Limit"] = "10",
        };

    private static String BuildWithoutSslRequirement(String connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            SslMode = SslMode.Disable,
        }.ConnectionString;

    private static Task InsertFailedRowAsync(String connectionString) =>
        new DatabaseProbe(connectionString).ExecuteAsync(
            InsertFailedRowSql,
            new { Id = MessageId, FailedOn, LastError = "Pong.Rejected: Pong refused this ping" });

    private static async Task<QueueRow> ReadInboxRowAsync(String connectionString)
    {
        var inboxRows = await new DatabaseProbe(connectionString).ReadQueueRowsAsync("pongs", "inbox_messages");

        return inboxRows.ShouldHaveSingleItem();
    }

    private static Task<IReadOnlyList<String>> ReadModuleRolesAsync(String connectionString) =>
        new DatabaseProbe(connectionString).QueryAsync<String>(
            ModuleRolesSql,
            new { Prefix = $"{new NpgsqlConnectionStringBuilder(connectionString).Database}__" });

    private async Task<String> CreatePreparedDatabaseAsync()
    {
        var connectionString = await CreateDatabaseAsync();
        using var environmentOverride = new EnvironmentOverride(CreateEnvironment(connectionString, Only));

        var exitCode = await MonolithHost.RunAsync([], AddServicesOfCore102Program);

        exitCode.ShouldBe(Done);

        return connectionString;
    }

    private Task<String> CreateDatabaseAsync() =>
        TestHostEnvironment.CreateDatabaseAsync(postgresFixture.ConnectionString, "startup");

    private sealed class HostStartProbe(
        IServiceProvider serviceProvider,
        TaskCompletionSource<IServiceProvider> taskCompletionSource)
        : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = serviceProvider
                .GetRequiredService<IHostApplicationLifetime>().ApplicationStarted
                .Register(() => taskCompletionSource.TrySetResult(serviceProvider));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SettingsValidationProbe;
}
