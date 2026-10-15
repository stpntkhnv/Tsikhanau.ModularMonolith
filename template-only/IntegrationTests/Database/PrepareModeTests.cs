using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.App;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.IntegrationTests.Database;

public sealed class PrepareModeTests(PostgresFixture postgresFixture)
{
    private const String OnStartup = "OnStartup";

    private const String Only = "Only";

    private const String Never = "Never";

    private const String RunOnlyFirst = "run the image once with App:Database:PrepareMode=Only";

    private const String ModuleSchemasSql = """
        SELECT nspname AS "Schema", pg_get_userbyid(nspowner) AS "Owner"
        FROM pg_namespace WHERE nspname IN ('pings', 'pongs')
        ORDER BY nspname
        """;

    private const String ModuleRolesSql =
        "SELECT rolname FROM pg_roles WHERE starts_with(rolname, @Prefix) ORDER BY rolname";

    private const String PingsMigrationsSql =
        "SELECT migration_id FROM pings.__ef_migrations_history ORDER BY migration_id";

    private const String PongsMigrationsSql =
        "SELECT migration_id FROM pongs.__ef_migrations_history ORDER BY migration_id";

    private const String ForgetLatestPingsMigrationSql = """
        DELETE FROM pings.__ef_migrations_history
        WHERE migration_id = (SELECT max(migration_id) FROM pings.__ef_migrations_history)
        """;

    private const String NotBase64 = "not base64 at all";

    private const String LoginKeyRequired = "App:Database:LoginKey is required";

    private const String LoginKeyNotBase64 = "App:Database:LoginKey must be base64";

    private const String OlderServerImage = "postgres:16-alpine";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Only_prepares_the_database_and_exits_without_listening_or_starting_background_work()
    {
        var connectionString = await CreateDatabaseAsync(postgresFixture.ConnectionString);
        var databaseName = ReadDatabaseName(connectionString);
        using var takenPort = new TcpListener(IPAddress.Loopback, 0);
        takenPort.Start();
        var environment = CreateEnvironment(Only, connectionString);
        environment["ASPNETCORE_URLS"] = BuildUrl(((IPEndPoint)takenPort.LocalEndpoint).Port);
        var startRecordPath = environment["TestHost__StartRecordPath"];

        using var testHost = TestHostProcess.Start(environment);
        var exit = await testHost.WaitForExitAsync();

        var hostStarted = File.Exists(startRecordPath);
        var databaseProbe = new DatabaseProbe(connectionString);
        var schemaOwners = await databaseProbe.QueryAsync<SchemaOwner>(ModuleSchemasSql);
        var roles = await databaseProbe.QueryAsync<String>(ModuleRolesSql, new { Prefix = $"{databaseName}__" });
        var pingsMigrations = await databaseProbe.QueryAsync<String>(PingsMigrationsSql);
        var pongsMigrations = await databaseProbe.QueryAsync<String>(PongsMigrationsSql);

        exit.Code.ShouldBe(0, exit.Text);
        exit.Output.ShouldContain($"Prepared the database {databaseName} for the modules Pings, Pongs");
        exit.Output.ShouldContain("App:Database:PrepareMode is Only: the database is prepared");
        exit.Output.ShouldNotContain("Now listening on");
        hostStarted.ShouldBeFalse();
        schemaOwners.ShouldBe([new("pings", $"{databaseName}__pings"), new("pongs", $"{databaseName}__pongs")]);
        roles.ShouldBe([$"{databaseName}__modules", $"{databaseName}__pings", $"{databaseName}__pongs"]);
        pingsMigrations.ShouldBe(ReadMigrations(new PingsDbContextDesignTimeFactory().CreateDbContext([])));
        pongsMigrations.ShouldBe(ReadMigrations(new PongsDbContextDesignTimeFactory().CreateDbContext([])));
    }

    [Fact]
    public async Task Never_runs_without_admin_credentials_on_a_database_prepared_by_only()
    {
        var connectionString = await CreatePreparedDatabaseAsync();
        var environment = CreateEnvironment(Never, BuildWithoutCredentials(connectionString));
        var url = environment["ASPNETCORE_URLS"];
        var startRecordPath = environment["TestHost__StartRecordPath"];

        using var testHost = TestHostProcess.Start(environment);
        await testHost.WaitUntilHealthyAsync(url);
        using var httpClient = new HttpClient();
        var health = await httpClient.GetStringAsync($"{url}/health", CancellationToken);
        using var createResponse = await httpClient.PostAsync(
            $"{url}/pings",
            new StringContent("""{"label":"without admin credentials"}""", Encoding.UTF8, "application/json"),
            CancellationToken);

        var hostStarted = File.Exists(startRecordPath);
        File.Delete(startRecordPath);

        health.ShouldBe("Healthy");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        hostStarted.ShouldBeTrue();
    }

    [Fact]
    public async Task Never_refuses_a_database_that_was_never_prepared()
    {
        var connectionString = await CreateDatabaseAsync(postgresFixture.ConnectionString);
        var databaseName = ReadDatabaseName(connectionString);

        var exit = await RunAsync(Never, BuildWithoutCredentials(connectionString));

        exit.Code.ShouldNotBe(0);
        exit.Text.ShouldContain($"Module Pings cannot sign in as {databaseName}__pings");
        exit.Text.ShouldContain(RunOnlyFirst);
    }

    [Fact]
    public async Task Never_refuses_a_migration_the_database_does_not_have()
    {
        var connectionString = await CreatePreparedDatabaseAsync();
        var latestPingsMigration = ReadMigrations(new PingsDbContextDesignTimeFactory().CreateDbContext([]))[^1];
        await new DatabaseProbe(connectionString).ExecuteAsync(ForgetLatestPingsMigrationSql);

        var exit = await RunAsync(Never, BuildWithoutCredentials(connectionString));

        exit.Code.ShouldNotBe(0);
        exit.Text.ShouldContain(
            $"Module Pings has migrations the database does not have: {latestPingsMigration}");

        exit.Text.ShouldContain(RunOnlyFirst);
    }

    [Fact]
    public async Task Never_refuses_model_changes_that_no_migration_holds()
    {
        var connectionString = await CreatePreparedDatabaseAsync();
        var environment = CreateEnvironment(Never, BuildWithoutCredentials(connectionString));
        environment["TestHost__AddUnmigratedProperty"] = "true";

        using var testHost = TestHostProcess.Start(environment);
        var exit = await testHost.WaitForExitAsync();

        exit.Code.ShouldNotBe(0);
        exit.Text.ShouldContain("Module Pings has model changes that no migration holds");
        exit.Text.ShouldContain("bash scripts/add-migration.sh Pings <Name>");
    }

    [Fact]
    public async Task Never_refuses_a_database_connection_string_with_a_password()
    {
        var connectionString = await CreateDatabaseAsync(postgresFixture.ConnectionString);

        var exit = await RunAsync(Never, connectionString);

        exit.Code.ShouldNotBe(0);
        exit.Text.ShouldContain(
            "Connection string 'database' holds a password, but App:Database:PrepareMode is Never");
    }

    [Fact]
    public async Task Never_refuses_a_budget_above_what_the_server_leaves_to_ordinary_logins()
    {
        var connectionString = await CreatePreparedDatabaseAsync();
        var environment = CreateEnvironment(Never, BuildWithoutCredentials(connectionString));
        environment["App__Database__MaxConnections"] = "5000";

        using var testHost = TestHostProcess.Start(environment);
        var exit = await testHost.WaitForExitAsync();

        exit.Code.ShouldNotBe(0);
        exit.Text.ShouldContain("App:Database:MaxConnections is 5000; it must be at least 4");
        exit.Text.ShouldContain("connections the server leaves to ordinary logins");
    }

    [Fact]
    public async Task Never_refuses_a_budget_below_two_connections_per_module()
    {
        var connectionString = await CreatePreparedDatabaseAsync();
        var environment = CreateEnvironment(Never, BuildWithoutCredentials(connectionString));
        environment["App__Database__MaxConnections"] = "3";

        using var testHost = TestHostProcess.Start(environment);
        var exit = await testHost.WaitForExitAsync();

        exit.Code.ShouldNotBe(0);
        exit.Text.ShouldContain(
            "App:Database:MaxConnections is 3; it must be at least 4 (2 for each of the 2 modules)");
    }

    [Fact]
    public async Task Never_refuses_a_server_older_than_postgresql_17_through_the_module_logins()
    {
        await using var olderServer = new PostgreSqlBuilder(OlderServerImage).Build();
        await olderServer.StartAsync(CancellationToken);
        var connectionString = await CreateDatabaseAsync(olderServer.GetConnectionString());
        await CreateModuleLoginsAsync(connectionString);

        var exit = await RunAsync(Never, BuildWithoutCredentials(connectionString));

        exit.Code.ShouldNotBe(0);
        exit.Text.ShouldContain("The database server reports server_version_num 16");
        exit.Text.ShouldContain("the app needs PostgreSQL 17 or later");
    }

    [Theory]
    [InlineData(OnStartup, "", LoginKeyRequired)]
    [InlineData(Only, "", LoginKeyRequired)]
    [InlineData(OnStartup, NotBase64, LoginKeyNotBase64)]
    [InlineData(Only, NotBase64, LoginKeyNotBase64)]
    public async Task Missing_or_invalid_login_key_stops_a_mode_that_prepares(
        String prepareMode,
        String loginKey,
        String refusal)
    {
        var environment = CreateEnvironment(prepareMode, BuildWithClosedPort(postgresFixture.ConnectionString));
        environment["App__Database__LoginKey"] = loginKey;

        using var testHost = TestHostProcess.Start(environment);
        var exit = await testHost.WaitForExitAsync();

        exit.Code.ShouldNotBe(0);
        exit.Text.ShouldContain(refusal);
    }

    [Theory]
    [InlineData("", LoginKeyRequired)]
    [InlineData(NotBase64, LoginKeyNotBase64)]
    public async Task Missing_or_invalid_login_key_stops_never(String loginKey, String refusal)
    {
        var connectionString = BuildWithoutCredentials(BuildWithClosedPort(postgresFixture.ConnectionString));
        var environment = CreateEnvironment(Never, connectionString);
        environment["App__Database__LoginKey"] = loginKey;

        using var testHost = TestHostProcess.Start(environment);
        var exit = await testHost.WaitForExitAsync();

        exit.Code.ShouldNotBe(0);
        exit.Text.ShouldContain(refusal);
    }

    private static Dictionary<String, String> CreateEnvironment(String prepareMode, String connectionString) =>
        new()
        {
            ["ASPNETCORE_ENVIRONMENT"] = Environments.Production,
            ["DOTNET_ENVIRONMENT"] = Environments.Production,
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

    private static async Task<TestHostExit> RunAsync(String prepareMode, String connectionString)
    {
        using var testHost = TestHostProcess.Start(CreateEnvironment(prepareMode, connectionString));
        return await testHost.WaitForExitAsync();
    }

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

    private static String BuildWithClosedPort(String connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Port = GetFreePort(),
            SslMode = SslMode.Disable,
        }.ConnectionString;

    private static String BuildUrl(Int32 port) => $"http://127.0.0.1:{port}";

    private static Int32 GetFreePort()
    {
        using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        return ((IPEndPoint)tcpListener.LocalEndpoint).Port;
    }

    private static String ReadDatabaseName(String connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString).Database ?? String.Empty;

    private static IReadOnlyList<String> ReadMigrations(DbContext dbContext)
    {
        using (dbContext)
        {
            return [.. dbContext.Database.GetMigrations()];
        }
    }

    private static async Task<String> CreateDatabaseAsync(String serverConnectionString)
    {
        var databaseName = $"prepare_mode_{Guid.CreateVersion7():N}";
        await new DatabaseProbe(serverConnectionString).ExecuteAsync($"CREATE DATABASE {databaseName}");
        return new NpgsqlConnectionStringBuilder(serverConnectionString)
        {
            Database = databaseName,
            SslMode = SslMode.Disable,
        }.ConnectionString;
    }

    private static async Task CreateModuleLoginsAsync(String connectionString)
    {
        var loginNames = new ModuleLoginNames(ReadDatabaseName(connectionString));
        var databaseProbe = new DatabaseProbe(connectionString);
        foreach (var schema in new[] { PingsDbContext.SchemaName, PongsDbContext.SchemaName })
        {
            var login = ModuleLogin.Create(loginNames, schema, TestSettings.LoginKey);
            await databaseProbe.ExecuteAsync($"CREATE ROLE \"{login.Name}\" LOGIN PASSWORD '{login.Password}'");
        }
    }

    private async Task<String> CreatePreparedDatabaseAsync()
    {
        var connectionString = await CreateDatabaseAsync(postgresFixture.ConnectionString);
        var exit = await RunAsync(Only, connectionString);

        exit.Code.ShouldBe(0, exit.Text);

        return connectionString;
    }

    private sealed record SchemaOwner(String Schema, String Owner);
}
