using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.App;
using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Database;

public sealed class ProvisionerTests(PostgresFixture postgresFixture)
{
    private const String OwnerSql = "SELECT pg_get_userbyid(nspowner) FROM pg_namespace WHERE nspname = @Schema";
    private const Int32 ParallelStartRounds = 5;

    private const String RaceOwnersSql = """
        SELECT nspname AS "Schema", pg_get_userbyid(nspowner) AS "Owner"
        FROM pg_namespace WHERE nspname IN ('race_a', 'race_b')
        ORDER BY nspname
        """;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Parallel_starts_on_different_databases_all_provision()
    {
        for (var round = 0; round < ParallelStartRounds; round++)
        {
            var connectionStrings = new[] { await CreateDatabaseAsync(), await CreateDatabaseAsync() };

            await Task.WhenAll(connectionStrings.Select(ProvisionRaceLoginsAsync));

            foreach (var connectionString in connectionStrings)
            {
                var databaseName = ReadDatabaseName(connectionString);
                var schemaOwners = await new DatabaseProbe(connectionString).QueryAsync<SchemaOwner>(RaceOwnersSql);

                schemaOwners.ShouldBe(
                    [new("race_a", $"{databaseName}__race_a"), new("race_b", $"{databaseName}__race_b")]);
            }
        }
    }

    [Fact]
    public async Task Createrole_admin_with_database_create_provisions_its_own_logins()
    {
        var connectionString = await CreateDatabaseAsync();
        var creatorConnectionString = await CreateAdminLoginAsync(
            "creator_ok",
            connectionString,
            canCreateSchemas: true);

        var login = CreateLogin(connectionString, "creator_probe");

        await ProvisionAsync(creatorConnectionString, [login], maxConnections: 10);

        var owner = await new DatabaseProbe(connectionString).ExecuteScalarAsync<String>(
            OwnerSql,
            new { Schema = "creator_probe" });

        owner.ShouldBe($"{ReadDatabaseName(connectionString)}__creator_probe");
    }

    [Fact]
    public async Task Createrole_admin_without_database_create_is_refused()
    {
        var connectionString = await CreateDatabaseAsync();
        var creatorConnectionString = await CreateAdminLoginAsync(
            "creator_no_create",
            connectionString,
            canCreateSchemas: false);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => ProvisionAsync(
                creatorConnectionString,
                [CreateLogin(connectionString, "nocreate_probe")],
                maxConnections: 10));

        exception.Message.ShouldContain("CREATE on the database");
    }

    [Fact]
    public async Task Admin_without_the_admin_option_on_an_existing_login_is_refused()
    {
        var connectionString = await CreateDatabaseAsync();
        var foreignLogin = CreateLogin(connectionString, "foreign_probe");
        await new DatabaseProbe(postgresFixture.ConnectionString).ExecuteAsync(
            $"CREATE ROLE {foreignLogin.Name} LOGIN");

        var creatorConnectionString = await CreateAdminLoginAsync(
            "creator_foreign",
            connectionString,
            canCreateSchemas: true);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => ProvisionAsync(
                creatorConnectionString,
                [foreignLogin],
                maxConnections: 10));

        exception.Message.ShouldContain("ADMIN option");
    }

    [Fact]
    public async Task Connection_budget_above_the_server_limit_is_refused()
    {
        var connectionString = await CreateDatabaseAsync();
        var adminConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        }.ConnectionString;

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => ProvisionAsync(
                adminConnectionString,
                [CreateLogin(connectionString, "budget_probe")],
                maxConnections: 5000));

        exception.Message.ShouldContain("MaxConnections");
    }

    [Fact]
    public async Task Connection_budget_below_two_per_module_is_refused()
    {
        var connectionString = await CreateDatabaseAsync();
        var adminConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        }.ConnectionString;

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => ProvisionAsync(
                adminConnectionString,
                [CreateLogin(connectionString, "budget_a"), CreateLogin(connectionString, "budget_b")],
                maxConnections: 3));

        exception.Message.ShouldContain(
            "App:Database:MaxConnections is 3; it must be at least 4 (2 for each of the 2 modules)");
    }

    [Fact]
    public async Task Connection_budget_of_exactly_two_per_module_is_accepted()
    {
        var connectionString = await CreateDatabaseAsync();
        var adminConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        }.ConnectionString;

        await Should.NotThrowAsync(
            () => ProvisionAsync(
                adminConnectionString,
                [CreateLogin(connectionString, "budget_exact_a"), CreateLogin(connectionString, "budget_exact_b")],
                maxConnections: 4));
    }

    private static ModuleLoginNames CreateLoginNames(String connectionString) =>
        new(ReadDatabaseName(connectionString));

    private static ModuleLogin CreateLogin(String connectionString, String schema) =>
        ModuleLogin.Create(CreateLoginNames(connectionString), schema, TestSettings.LoginKey);

    private static String ReadDatabaseName(String connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString).Database ?? String.Empty;

    private static Task ProvisionRaceLoginsAsync(String connectionString)
    {
        var adminConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        }.ConnectionString;

        return ProvisionAsync(
            adminConnectionString,
            [CreateLogin(connectionString, "race_a"), CreateLogin(connectionString, "race_b")],
            maxConnections: 10);
    }

    private static async Task ProvisionAsync(
        String adminConnectionString,
        IReadOnlyList<ModuleLogin> logins,
        Int32 maxConnections)
    {
        await using var adminSession = await AdminSession.OpenLockedAsync(adminConnectionString, CancellationToken);
        await ModuleDatabaseProvisioner.ProvisionAsync(
            adminSession.DataSource,
            CreateLoginNames(adminConnectionString).Group,
            logins,
            maxConnections,
            CancellationToken);
    }

    private async Task<String> CreateDatabaseAsync()
    {
        var databaseName = $"provisioner_{Guid.CreateVersion7():N}";
        await new DatabaseProbe(postgresFixture.ConnectionString).ExecuteAsync($"CREATE DATABASE {databaseName}");
        return new NpgsqlConnectionStringBuilder(postgresFixture.ConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;
    }

    private async Task<String> CreateAdminLoginAsync(
        String roleName,
        String connectionString,
        Boolean canCreateSchemas)
    {
        var superuserDatabaseProbe = new DatabaseProbe(postgresFixture.ConnectionString);
        await superuserDatabaseProbe.ExecuteAsync(
            $"DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{roleName}') "
            + $"THEN CREATE ROLE {roleName} LOGIN CREATEROLE PASSWORD '{roleName}'; END IF; END $$");

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Username = roleName,
            Password = roleName,
            Pooling = false,
        };

        if (canCreateSchemas)
        {
            await superuserDatabaseProbe.ExecuteAsync(
                $"GRANT CREATE ON DATABASE {connectionStringBuilder.Database} TO {roleName}");
        }

        return connectionStringBuilder.ConnectionString;
    }

    private sealed record SchemaOwner(String Schema, String Owner);
}
