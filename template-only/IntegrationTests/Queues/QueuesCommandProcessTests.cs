using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
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

    private const String NotPrepared = "the database is not prepared for this app and its App:Database:LoginKey";

    private const String RunOnlyFirst = "run the image once with App:Database:PrepareMode=Only";

    private const String OutputPathVariable = "TESTHOST_OUTPUT_PATH";

    private const String WritesBetweenTwoLines =
        "ulimit -c 0 && { echo before && \"$0\" \"$@\" && echo after; } > \"$TESTHOST_OUTPUT_PATH\"";

    private const String UnixOnly = "the test redirects the output of the test host with a Unix shell";

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
        environment[TestHostEnvironment.UrlsKey] = TestHostEnvironment.BuildUrl(
            ((IPEndPoint)takenPort.LocalEndpoint).Port);


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
        exit.Error.ShouldContain($"queues: Module Pings cannot sign in as {databaseName}__pings");
        exit.Error.ShouldContain(NotPrepared);
        exit.Error.ShouldContain(RunOnlyFirst);
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

    [Fact]
    public async Task Discard_into_a_closed_pipe_fails_and_keeps_the_row()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), UnixOnly);
        var connectionString = await CreatePreparedDatabaseAsync();
        await InsertFailedRowAsync(connectionString);
        var environment = CreateEnvironment(Never, BuildWithoutCredentials(connectionString), Environments.Production);

        using var testHost = TestHostProcess.StartWithClosedOutput(
            environment,
            ["queues", "discard", "Pongs", "inbox_messages", MessageId]);

        var exit = await testHost.WaitForExitAsync();

        var inboxRows = await new DatabaseProbe(connectionString).ReadQueueRowsAsync("pongs", "inbox_messages");

        exit.Code.ShouldBe(1, exit.Text);
        exit.Error.ShouldContain("queues: Broken pipe");
        inboxRows
            .ShouldHaveSingleItem()
            .HasFailed
            .ShouldBeTrue();
    }

    [Fact]
    public async Task List_into_a_file_that_a_script_writes_around_it_keeps_every_line_in_order()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), UnixOnly);
        var connectionString = await CreatePreparedDatabaseAsync();
        await InsertFailedRowAsync(connectionString);
        var environment = CreateEnvironment(Never, BuildWithoutCredentials(connectionString), Environments.Production);
        var outputPath = Path.Combine(Path.GetTempPath(), $"queues-list-{Guid.CreateVersion7():N}");
        environment[OutputPathVariable] = outputPath;
        try
        {
            using var testHost = TestHostProcess.StartInShell(WritesBetweenTwoLines, environment, ["queues", "list"]);
            var exit = await testHost.WaitForExitAsync();

            var lines = await File.ReadAllLinesAsync(outputPath, TestContext.Current.CancellationToken);

            exit.Code.ShouldBe(0, exit.Text);
            lines.ShouldBe(["before", Header, FailedRow, "after"]);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    private static async Task<TestHostExit> RunAsync(Dictionary<String, String> environment, params String[] arguments)
    {
        using var testHost = TestHostProcess.Start(environment, arguments);
        return await testHost.WaitForExitAsync();
    }

    private static Dictionary<String, String> CreateEnvironment(
        String prepareMode,
        String connectionString,
        String environmentName)
    {
        var environment = TestHostEnvironment.Create(connectionString, environmentName);
        environment["App__Database__PrepareMode"] = prepareMode;
        environment["TestHost__StartRecordPath"] = BuildStartRecordPath();
        return environment;
    }

    private static String BuildStartRecordPath() =>
        Path.Combine(Path.GetTempPath(), $"started-{Guid.CreateVersion7():N}");

    private async Task<String> CreatePreparedDatabaseAsync()
    {
        var connectionString = await CreateDatabaseAsync();
        var exit = await RunAsync(CreateEnvironment(Only, connectionString, Environments.Production));

        exit.Code.ShouldBe(0, exit.Text);

        return connectionString;
    }

    private Task<String> CreateDatabaseAsync() =>
        TestHostEnvironment.CreateDatabaseAsync(postgresFixture.ConnectionString, "queues_command");

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
}
