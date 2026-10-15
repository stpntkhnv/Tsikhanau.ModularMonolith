using Microsoft.Extensions.Options;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Database;

public sealed class LoginKeyTests(PostgresFixture postgresFixture)
{
    private const String ModuleSchemasSql = "SELECT nspname FROM pg_namespace WHERE nspname IN ('pings', 'pongs')";

    private const String RotatingAdmin = "rotating_admin";

    private const String CreateRotatingAdminSql = $"""
        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{RotatingAdmin}') THEN
                CREATE ROLE {RotatingAdmin} LOGIN SUPERUSER;
            END IF;
        END $$
        """;

    private const String Label = "after the admin password changed";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Missing_login_key_stops_preparation_before_any_module_schema_exists()
    {
        var connectionString = await CreateDatabaseAsync();
        using var host = TestModulesHost.BuildUnstarted(
            connectionString,
            builder => builder.Configuration["App:Database:LoginKey"] = null);

        var exception = await Should.ThrowAsync<OptionsValidationException>(
            () => host.Services.PrepareModuleDatabasesAsync(CancellationToken));

        var moduleSchemas = await new DatabaseProbe(connectionString).QueryAsync<String>(ModuleSchemasSql);

        exception.Message.ShouldContain("App:Database:LoginKey is required");
        moduleSchemas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Running_instance_keeps_working_after_another_instance_prepares_with_a_new_admin_password()
    {
        var connectionString = await CreateDatabaseAsync();
        var firstAdminConnectionString = await SetRotatingAdminPasswordAsync(connectionString, "first-password");
        await using var runningHost = await TestModulesHost.StartAsync(firstAdminConnectionString);

        var newAdminConnectionString = await SetRotatingAdminPasswordAsync(connectionString, "new-password");
        await PrepareAsync(newAdminConnectionString);
        runningHost.ClearModulePools();

        using var httpClient = runningHost.CreateClient();
        var healthResponse = await httpClient.GetAsync("/health", CancellationToken);
        var health = await healthResponse.Content.ReadAsStringAsync(CancellationToken);
        var pingId = await runningHost.CreatePingAsync(Label);
        var ping = await runningHost.GetPingAsync(pingId);

        health.ShouldBe("Healthy");
        ping.Label.ShouldBe(Label);
    }

    private static async Task PrepareAsync(String connectionString)
    {
        using var host = TestModulesHost.BuildUnstarted(connectionString);
        await host.Services.PrepareModuleDatabasesAsync(CancellationToken);
    }

    private async Task<String> SetRotatingAdminPasswordAsync(String connectionString, String password)
    {
        var superuserDatabaseProbe = new DatabaseProbe(postgresFixture.ConnectionString);
        await superuserDatabaseProbe.ExecuteAsync(CreateRotatingAdminSql);
        await superuserDatabaseProbe.ExecuteAsync($"ALTER ROLE {RotatingAdmin} PASSWORD '{password}'");

        return new NpgsqlConnectionStringBuilder(connectionString)
        {
            Username = RotatingAdmin,
            Password = password,
        }.ConnectionString;
    }

    private async Task<String> CreateDatabaseAsync()
    {
        var databaseName = $"login_key_{Guid.CreateVersion7():N}";
        await new DatabaseProbe(postgresFixture.ConnectionString).ExecuteAsync($"CREATE DATABASE {databaseName}");
        return new NpgsqlConnectionStringBuilder(postgresFixture.ConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;
    }
}
