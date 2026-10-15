using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.App;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.Database;

public sealed class SharedServerTests(PostgresFixture postgresFixture)
{
    private const String SchemaOwnersSql = """
        SELECT nspname AS "Schema", pg_get_userbyid(nspowner) AS "Owner"
        FROM pg_namespace WHERE nspname IN ('pings', 'pongs')
        ORDER BY nspname
        """;

    private const String CurrentDatabaseSql = "SELECT current_database()";

    private const String ReadPingsSql = "SELECT count(*) FROM pings.pings";

    private const String WritePingsSql = "DELETE FROM pings.pings";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_databases_prepared_by_two_admins_on_one_server_both_start_with_their_own_roles()
    {
        var firstDatabase = await CreateDatabaseWithItsOwnAdminAsync();
        var secondDatabase = await CreateDatabaseWithItsOwnAdminAsync();
        using var firstHost = TestModulesHost.BuildUnstarted(firstDatabase.ConnectionString);
        using var secondHost = TestModulesHost.BuildUnstarted(secondDatabase.ConnectionString);

        await firstHost.Services.PrepareModuleDatabasesAsync(CancellationToken);
        await secondHost.Services.PrepareModuleDatabasesAsync(CancellationToken);

        var firstOwners = await ReadSchemaOwnersAsync(firstDatabase.Name);
        var secondOwners = await ReadSchemaOwnersAsync(secondDatabase.Name);
        var firstPings = await CountPingsAsModuleAsync(firstHost);
        var secondPings = await CountPingsAsModuleAsync(secondHost);

        firstOwners.ShouldBe(
            [new("pings", $"{firstDatabase.Name}__pings"), new("pongs", $"{firstDatabase.Name}__pongs")]);

        secondOwners.ShouldBe(
            [new("pings", $"{secondDatabase.Name}__pings"), new("pongs", $"{secondDatabase.Name}__pongs")]);

        firstPings.ShouldBe(0);
        secondPings.ShouldBe(0);
    }

    [Fact]
    public async Task Login_of_one_database_connects_to_another_but_neither_reads_nor_writes_its_module_tables()
    {
        var firstDatabase = await CreateDatabaseWithItsOwnAdminAsync();
        var secondDatabase = await CreateDatabaseWithItsOwnAdminAsync();
        using (var firstHost = TestModulesHost.BuildUnstarted(firstDatabase.ConnectionString))
        {
            await firstHost.Services.PrepareModuleDatabasesAsync(CancellationToken);
        }

        using (var secondHost = TestModulesHost.BuildUnstarted(secondDatabase.ConnectionString))
        {
            await secondHost.Services.PrepareModuleDatabasesAsync(CancellationToken);
            await CreatePingAsync(secondHost, "kept");
        }

        var crossDatabaseProbe = new DatabaseProbe(
            ConnectAsFirstPingsLogin(firstDatabase.Name, secondDatabase.ConnectionString));

        var connectedDatabase = await crossDatabaseProbe.ExecuteScalarAsync<String>(CurrentDatabaseSql);
        var readException = await Should.ThrowAsync<PostgresException>(
            () => crossDatabaseProbe.ExecuteScalarAsync<Int64>(ReadPingsSql));

        var writeException = await Should.ThrowAsync<PostgresException>(
            () => crossDatabaseProbe.ExecuteAsync(WritePingsSql));

        var keptPings = await new DatabaseProbe(ConnectAsSuperuser(secondDatabase.Name)).ExecuteScalarAsync<Int64>(
            ReadPingsSql);

        connectedDatabase.ShouldBe(secondDatabase.Name);
        readException.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        writeException.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        keptPings.ShouldBe(1);
    }

    private static async Task<Int64> CountPingsAsModuleAsync(IHost host)
    {
        var moduleConnectionProbe = new ModuleConnectionProbe(
            host.Services.GetRequiredKeyedService<NpgsqlDataSource>(typeof(IPingsModule)));

        return await moduleConnectionProbe.ExecuteScalarAsync<Int64>(ReadPingsSql);
    }

    private static async Task CreatePingAsync(IHost host, String label)
    {
        var result = await host.Services
            .GetRequiredService<IPingsModule>()
            .ExecuteCommandAsync(
                new CreatePingCommand(Guid.CreateVersion7(), label, Optional<String>.None()),
                CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    private static String ConnectAsFirstPingsLogin(String firstDatabaseName, String secondConnectionString)
    {
        var firstPingsLogin = ModuleLogin.Create(
            new ModuleLoginNames(firstDatabaseName),
            "pings",
            TestSettings.LoginKey);

        return new NpgsqlConnectionStringBuilder(secondConnectionString)
        {
            Username = firstPingsLogin.Name,
            Password = firstPingsLogin.Password,
            Pooling = false,
        }.ConnectionString;
    }

    private Task<IReadOnlyList<SchemaOwner>> ReadSchemaOwnersAsync(String databaseName) =>
        new DatabaseProbe(ConnectAsSuperuser(databaseName)).QueryAsync<SchemaOwner>(SchemaOwnersSql);

    private String ConnectAsSuperuser(String databaseName) =>
        new NpgsqlConnectionStringBuilder(postgresFixture.ConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;

    private async Task<SharedServerDatabase> CreateDatabaseWithItsOwnAdminAsync()
    {
        var id = Guid.CreateVersion7();
        var databaseName = $"shared_{id:N}";
        var adminName = $"admin_{id:N}";
        var superuserDatabaseProbe = new DatabaseProbe(postgresFixture.ConnectionString);
        await superuserDatabaseProbe.ExecuteAsync($"CREATE ROLE {adminName} LOGIN CREATEROLE PASSWORD '{adminName}'");
        await superuserDatabaseProbe.ExecuteAsync($"CREATE DATABASE {databaseName}");
        await superuserDatabaseProbe.ExecuteAsync($"GRANT CREATE ON DATABASE {databaseName} TO {adminName}");

        var connectionString = new NpgsqlConnectionStringBuilder(ConnectAsSuperuser(databaseName))
        {
            Username = adminName,
            Password = adminName,
        }.ConnectionString;

        return new SharedServerDatabase(databaseName, connectionString);
    }

    private sealed record SharedServerDatabase(String Name, String ConnectionString);

    private sealed record SchemaOwner(String Schema, String Owner);
}
