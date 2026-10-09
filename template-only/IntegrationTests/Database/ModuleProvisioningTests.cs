using ModularMonolith.Testing.Database;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.IntegrationTests.Hosting;

namespace Tsikhanau.Monolith.IntegrationTests.Database;

public sealed class ModuleProvisioningTests(PostgresFixture postgresFixture)
{
    private const String OwnerSql = "SELECT pg_get_userbyid(nspowner) FROM pg_namespace WHERE nspname = @Schema";

    private const String ObjectsOwnedByOthersSql = """
        SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = @Schema AND pg_get_userbyid(c.relowner) <> @Login
        """;

    private const String SuperuserSql = "SELECT rolsuper FROM pg_roles WHERE rolname = @Login";

    private const String CreatePlainLoginSql = """
        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plain') THEN
                CREATE ROLE plain LOGIN PASSWORD 'plain';
            END IF;
        END $$
        """;

    private const String AdminOptionSql = """
        SELECT bool_or(admin_option) FROM pg_auth_members
        WHERE roleid = 'modularmonolith_modules'::regrole AND member = 'modularmonolith_pings'::regrole
        """;

    private const String StoredSecretSql = "SELECT rolpassword FROM pg_authid WHERE rolname = 'modularmonolith_pings'";

    private const Int32 ParallelStartRounds = 5;

    private const String ModuleOwnershipSql = """
        SELECT n.nspname AS "Schema", pg_get_userbyid(n.nspowner) AS "Owner",
               (SELECT count(*) FROM pg_class c WHERE c.relnamespace = n.oid AND c.relowner <> n.nspowner)
                   AS "ObjectsOwnedByOthers"
        FROM pg_namespace n WHERE n.nspname IN ('pings', 'pongs')
        ORDER BY n.nspname
        """;

    private const String InboxDeliverySql = """
        SELECT has_table_privilege('modularmonolith_pings', 'pongs.inbox_messages', 'INSERT')
           AND has_column_privilege('modularmonolith_pings', 'pongs.inbox_messages', 'id', 'SELECT')
           AND has_table_privilege('modularmonolith_pongs', 'pings.inbox_messages', 'INSERT')
           AND has_column_privilege('modularmonolith_pongs', 'pings.inbox_messages', 'id', 'SELECT')
        """;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("pings")]
    [InlineData("pongs")]
    public async Task Startup_gives_each_module_a_login_that_owns_its_schema(String schema)
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var loginName = ModuleLogin.BuildName(schema);

        var owner = await testModulesHost.Database.ExecuteScalarAsync<String>(OwnerSql, new { Schema = schema });
        var objectsOwnedByOthers = await testModulesHost.Database.ExecuteScalarAsync<Int64>(
            ObjectsOwnedByOthersSql,
            new { Schema = schema, Login = loginName });

        var isSuperuser = await testModulesHost.Database.ExecuteScalarAsync<Boolean>(
            SuperuserSql,
            new { Login = loginName });

        owner.ShouldBe(loginName);
        objectsOwnedByOthers.ShouldBe(0);
        isSuperuser.ShouldBeFalse();
    }

    [Fact]
    public async Task Second_startup_changes_nothing()
    {
        await using (await TestModulesHost.StartAsync(postgresFixture))
        {
        }

        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("again");

        pingId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Startup_restores_a_changed_module_password()
    {
        await using (await TestModulesHost.StartAsync(postgresFixture))
        {
        }

        await new DatabaseProbe(postgresFixture.ConnectionString).ExecuteAsync(
            "ALTER ROLE modularmonolith_pings PASSWORD 'stale'");

        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("restored");

        pingId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Schema_with_another_owner_stops_startup()
    {
        var connectionString = await CreateDatabaseAsync();
        await new DatabaseProbe(connectionString).ExecuteAsync("CREATE SCHEMA pings");

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => TestModulesHost.StartAsync(connectionString));

        exception.Message.ShouldContain("pings");
        exception.Message.ShouldContain("Drop the old development database");
    }

    [Fact]
    public async Task Table_with_another_owner_stops_startup()
    {
        var connectionString = await CreateDatabaseAsync();
        await using (await TestModulesHost.StartAsync(connectionString))
        {
        }

        await new DatabaseProbe(connectionString).ExecuteAsync("CREATE TABLE pings.stray (id int)");

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => TestModulesHost.StartAsync(connectionString));

        exception.Message.ShouldContain("stray");
    }

    [Fact]
    public async Task Admin_that_cannot_create_roles_stops_startup()
    {
        var connectionString = await CreateDatabaseAsync();
        await new DatabaseProbe(postgresFixture.ConnectionString).ExecuteAsync(CreatePlainLoginSql);

        var plainLoginConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Username = "plain",
            Password = "plain",
        };

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => TestModulesHost.StartAsync(plainLoginConnectionStringBuilder.ConnectionString));

        exception.Message.ShouldContain(
            "The login in ConnectionStrings:database must be a superuser or have CREATEROLE to create module logins");
    }

    [Fact]
    public async Task Extra_parent_role_on_a_module_login_stops_startup()
    {
        await using (await TestModulesHost.StartAsync(postgresFixture))
        {
        }

        var superuserDatabaseProbe = new DatabaseProbe(postgresFixture.ConnectionString);
        await superuserDatabaseProbe.ExecuteAsync("GRANT modularmonolith_pongs TO modularmonolith_pings");
        try
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => TestModulesHost.StartAsync(postgresFixture));

            exception.Message.ShouldContain("modularmonolith_pings is a member of modularmonolith_pongs");
        }
        finally
        {
            await superuserDatabaseProbe.ExecuteAsync("REVOKE modularmonolith_pongs FROM modularmonolith_pings");
        }
    }

    [Fact]
    public async Task Group_that_can_log_in_stops_startup()
    {
        await using (await TestModulesHost.StartAsync(postgresFixture))
        {
        }

        var superuserDatabaseProbe = new DatabaseProbe(postgresFixture.ConnectionString);
        await superuserDatabaseProbe.ExecuteAsync("ALTER ROLE modularmonolith_modules LOGIN");
        try
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => TestModulesHost.StartAsync(postgresFixture));

            exception.Message.ShouldContain("modularmonolith_modules");
        }
        finally
        {
            await superuserDatabaseProbe.ExecuteAsync("ALTER ROLE modularmonolith_modules NOLOGIN");
        }
    }

    [Fact]
    public async Task Admin_option_on_the_group_membership_is_taken_back_at_startup()
    {
        await using (await TestModulesHost.StartAsync(postgresFixture))
        {
        }

        var superuserDatabaseProbe = new DatabaseProbe(postgresFixture.ConnectionString);
        await superuserDatabaseProbe.ExecuteAsync(
            "GRANT modularmonolith_modules TO modularmonolith_pings WITH ADMIN TRUE");

        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var hasAdminOption = await superuserDatabaseProbe.ExecuteScalarAsync<Boolean>(AdminOptionSql);

        hasAdminOption.ShouldBeFalse();
    }

    [Fact]
    public async Task Server_receives_only_a_scram_verifier_of_the_module_password()
    {
        var connectionString = await CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database;
        await new DatabaseProbe(connectionString).ExecuteAsync(
            $"ALTER DATABASE {databaseName} SET password_encryption = 'md5'");

        var wrongPasswordConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Username = "modularmonolith_pings",
            Password = "wrong",
        };

        await using var testModulesHost = await TestModulesHost.StartAsync(connectionString);
        var storedSecret = await testModulesHost.Database.ExecuteScalarAsync<String>(StoredSecretSql);
        var pingId = await testModulesHost.CreatePingAsync("verifier");
        var exception = await Should.ThrowAsync<PostgresException>(
            () => OpenConnectionAsync(wrongPasswordConnectionStringBuilder.ConnectionString));

        storedSecret.ShouldStartWith("SCRAM-SHA-256$4096:");
        pingId.ShouldNotBe(Guid.Empty);
        exception.SqlState.ShouldBe(PostgresErrorCodes.InvalidPassword);
    }

    [Fact]
    public async Task Parallel_full_starts_on_one_database_all_succeed()
    {
        for (var round = 0; round < ParallelStartRounds; round++)
        {
            var connectionString = await CreateDatabaseAsync();

            await PrepareTogetherAsync(connectionString);

            var databaseProbe = new DatabaseProbe(connectionString);
            var moduleOwnerships = await databaseProbe.QueryAsync<ModuleOwnership>(ModuleOwnershipSql);
            var canDeliverToInboxes = await databaseProbe.ExecuteScalarAsync<Boolean>(InboxDeliverySql);
            moduleOwnerships.ShouldBe(
                [new("pings", "modularmonolith_pings", 0), new("pongs", "modularmonolith_pongs", 0)]);

            canDeliverToInboxes.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Stray_group_grant_on_a_foreign_table_stops_startup()
    {
        await using (await TestModulesHost.StartAsync(postgresFixture))
        {
        }

        var superuserDatabaseProbe = new DatabaseProbe(postgresFixture.ConnectionString);
        await superuserDatabaseProbe.ExecuteAsync("GRANT SELECT ON pongs.outbox_messages TO modularmonolith_modules");
        try
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => TestModulesHost.StartAsync(postgresFixture));

            exception.Message.ShouldContain("modularmonolith_pings has SELECT on pongs.outbox_messages");
            exception.Message.ShouldContain("REVOKE SELECT ON pongs.outbox_messages FROM");
        }
        finally
        {
            await superuserDatabaseProbe.ExecuteAsync(
                "REVOKE SELECT ON pongs.outbox_messages FROM modularmonolith_modules");
        }
    }

    [Fact]
    public async Task Stray_column_grant_on_a_foreign_inbox_stops_startup()
    {
        await using (await TestModulesHost.StartAsync(postgresFixture))
        {
        }

        var superuserDatabaseProbe = new DatabaseProbe(postgresFixture.ConnectionString);
        await superuserDatabaseProbe.ExecuteAsync("GRANT SELECT (payload) ON pongs.inbox_messages TO PUBLIC");
        try
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => TestModulesHost.StartAsync(postgresFixture));

            exception.Message.ShouldContain("modularmonolith_pings has SELECT (payload) on pongs.inbox_messages");
        }
        finally
        {
            await superuserDatabaseProbe.ExecuteAsync("REVOKE SELECT (payload) ON pongs.inbox_messages FROM PUBLIC");
        }
    }

    [Fact]
    public async Task Database_create_for_a_module_login_stops_startup()
    {
        var connectionString = await CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database;
        await using (await TestModulesHost.StartAsync(connectionString))
        {
        }

        var superuserDatabaseProbe = new DatabaseProbe(connectionString);
        await superuserDatabaseProbe.ExecuteAsync($"GRANT CREATE ON DATABASE {databaseName} TO modularmonolith_pings");
        try
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => TestModulesHost.StartAsync(connectionString));

            exception.Message.ShouldContain($"modularmonolith_pings has CREATE on DATABASE {databaseName}");
        }
        finally
        {
            await superuserDatabaseProbe.ExecuteAsync(
                $"REVOKE CREATE ON DATABASE {databaseName} FROM modularmonolith_pings");
        }
    }

    private static async Task PrepareTogetherAsync(String connectionString)
    {
        using var firstHost = TestModulesHost.BuildUnstarted(connectionString);
        using var secondHost = TestModulesHost.BuildUnstarted(connectionString);

        await Task.WhenAll(
            firstHost.Services.PrepareModuleDatabasesAsync(CancellationToken),
            secondHost.Services.PrepareModuleDatabasesAsync(CancellationToken));
    }

    private static async Task OpenConnectionAsync(String connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken);
    }

    private async Task<String> CreateDatabaseAsync()
    {
        var databaseName = $"provisioning_{Guid.CreateVersion7():N}";
        await new DatabaseProbe(postgresFixture.ConnectionString).ExecuteAsync($"CREATE DATABASE {databaseName}");
        return new NpgsqlConnectionStringBuilder(postgresFixture.ConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;
    }

    private sealed record ModuleOwnership(String Schema, String Owner, Int64 ObjectsOwnedByOthers);
}
