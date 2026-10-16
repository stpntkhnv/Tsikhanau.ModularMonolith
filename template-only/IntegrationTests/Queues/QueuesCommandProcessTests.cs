using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.App;
using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

public sealed class QueuesCommandProcessTests(PostgresFixture postgresFixture)
{
    private const String OnStartup = "OnStartup";

    private const String Only = "Only";

    private const String Never = "Never";

    private const String Header = "module\tqueue\tid\ttype\tattempts\tfailed_on\tcode";

    private const String MessageId = "01999a00-0000-7000-8000-000000000001";

    private const String FailedRow =
        $"Pongs\tinbox_messages\t{MessageId}\tpings.ping-created\t1\t2026-09-28T08:00:00.0000000Z\tPong.Rejected";

    private const String InsertFailedRowSql = """
        INSERT INTO pongs.inbox_messages
            (id, type, payload, attempts, next_attempt_on, last_error, failed_on, occurred_on, received_on)
        VALUES (@Id::uuid, 'pings.ping-created', '{"label":"broken"}', 1, @FailedOn, @LastError, @FailedOn,
                @FailedOn, @FailedOn)
        """;

    private const String ModuleRolesSql = "SELECT rolname FROM pg_roles WHERE starts_with(rolname, @Prefix)";

    private const String UnhandledException = "Unhandled exception";

    private static readonly DateTimeOffset FailedOn = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_with_never_and_no_admin_password_prints_the_failed_rows_and_nothing_else_to_stdout()
    {
        var connectionString = await CreatePreparedDatabaseAsync();
        await InsertFailedRowAsync(connectionString);
        var environment = CreateEnvironment(Never, BuildWithoutCredentials(connectionString), Environments.Production);

        var exit = await RunAsync(environment, "queues", "list");

        exit.Code.ShouldBe(0, exit.Text);
        exit.Output.ShouldBe($"{Header}{Environment.NewLine}{FailedRow}{Environment.NewLine}");
        exit.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_with_the_development_configuration_starts_no_web_host_and_no_background_service()
    {
        var connectionString = await CreatePreparedDatabaseAsync();
        await InsertFailedRowAsync(connectionString);
        using var takenPort = new TcpListener(IPAddress.Loopback, 0);
        takenPort.Start();
        var environment = CreateEnvironment(OnStartup, connectionString, Environments.Development);
        environment["ASPNETCORE_URLS"] = BuildUrl(((IPEndPoint)takenPort.LocalEndpoint).Port);

        var exit = await RunAsync(environment, "queues", "list");

        var hostStarted = File.Exists(environment["TestHost__StartRecordPath"]);

        exit.Code.ShouldBe(0, exit.Text);
        exit.Output.ShouldBe($"{Header}{Environment.NewLine}{FailedRow}{Environment.NewLine}");
        hostStarted.ShouldBeFalse();
    }

    [Fact]
    public async Task List_with_the_development_configuration_prepares_nothing()
    {
        var connectionString = await CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database;
        var environment = CreateEnvironment(OnStartup, connectionString, Environments.Development);

        var exit = await RunAsync(environment, "queues", "list");

        var roles = await new DatabaseProbe(connectionString).QueryAsync<String>(
            ModuleRolesSql,
            new { Prefix = $"{databaseName}__" });

        var hostStarted = File.Exists(environment["TestHost__StartRecordPath"]);

        exit.Code.ShouldBe(1, exit.Text);
        exit.Error.ShouldContain($"queues: 28P01: password authentication failed for user \"{databaseName}__pings\"");
        exit.Error.ShouldNotContain(UnhandledException);
        roles.ShouldBeEmpty();
        hostStarted.ShouldBeFalse();
    }

    [Theory]
    [InlineData("App__Database__LoginKey", "", "App:Database:LoginKey is required")]
    [InlineData("App__Database__PrepareMode", "7", "App:Database:PrepareMode must be OnStartup, Only or Never")]
    public async Task Invalid_settings_fail_with_the_setting_named_on_stderr(
        String variable,
        String value,
        String refusal)
    {
        var environment = CreateEnvironment(OnStartup, postgresFixture.ConnectionString, Environments.Development);
        environment[variable] = value;

        var exit = await RunAsync(environment, "queues", "list");

        exit.Code.ShouldBe(1, exit.Text);
        exit.Output.ShouldBeEmpty();
        exit.Error.ShouldStartWith("queues: ");
        exit.Error.ShouldContain(refusal);
        exit.Error.ShouldNotContain(UnhandledException);
    }

    private static async Task<TestHostExit> RunAsync(Dictionary<String, String> environment, params String[] arguments)
    {
        using var testHost = TestHostProcess.Start(environment, arguments);
        return await testHost.WaitForExitAsync();
    }

    private static Dictionary<String, String> CreateEnvironment(
        String prepareMode,
        String connectionString,
        String environmentName) =>
        new()
        {
            ["ASPNETCORE_ENVIRONMENT"] = environmentName,
            ["DOTNET_ENVIRONMENT"] = environmentName,
            ["ASPNETCORE_URLS"] = BuildUrl(GetFreePort()),
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = String.Empty,
            ["ConnectionStrings__database"] = connectionString,
            ["App__Database__LoginKey"] = TestSettings.LoginKey,
            ["App__Database__PrepareMode"] = prepareMode,
            ["App__Queues__RunInBackground"] = "true",
            ["App__Cleanup__RunInBackground"] = "true",
            ["App__Modules__Pings__Limit"] = "10",
            ["App__Modules__Pongs__Limit"] = "10",
            ["TestHost__StartRecordPath"] = Path.Combine(Path.GetTempPath(), $"started-{Guid.CreateVersion7():N}"),
        };

    private async Task<String> CreatePreparedDatabaseAsync()
    {
        var connectionString = await CreateDatabaseAsync();
        var exit = await RunAsync(CreateEnvironment(Only, connectionString, Environments.Production));

        exit.Code.ShouldBe(0, exit.Text);

        return connectionString;
    }

    private async Task<String> CreateDatabaseAsync()
    {
        var databaseName = $"queues_command_{Guid.CreateVersion7():N}";
        await new DatabaseProbe(postgresFixture.ConnectionString).ExecuteAsync($"CREATE DATABASE {databaseName}");
        return new NpgsqlConnectionStringBuilder(postgresFixture.ConnectionString)
        {
            Database = databaseName,
            SslMode = SslMode.Disable,
        }.ConnectionString;
    }

    private static Task InsertFailedRowAsync(String connectionString) =>
        new DatabaseProbe(connectionString).ExecuteAsync(
            InsertFailedRowSql,
            new { Id = MessageId, FailedOn, LastError = "Pong.Rejected: Pong refused this ping" });

    private static String BuildWithoutCredentials(String connectionString)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        return new NpgsqlConnectionStringBuilder
        {
            Host = connectionStringBuilder.Host,
            Port = connectionStringBuilder.Port,
            Database = connectionStringBuilder.Database,
            SslMode = SslMode.Disable,
        }.ConnectionString;
    }

    private static String BuildUrl(Int32 port) => $"http://127.0.0.1:{port}";

    private static Int32 GetFreePort()
    {
        using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        return ((IPEndPoint)tcpListener.LocalEndpoint).Port;
    }
}
