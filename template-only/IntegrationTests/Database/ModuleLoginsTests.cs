using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.IntegrationTests.Database;

public sealed class ModuleLoginsTests(PostgresFixture postgresFixture)
{
    private const String GroupMembersSql = """
        SELECT r.rolname FROM pg_auth_members m JOIN pg_roles r ON r.oid = m.member
        WHERE m.roleid = to_regrole(@Group)
        ORDER BY r.rolname
        """;

    private const String DatabaseNameRule =
        "cannot name the module roles <database>__<schema>: it must be groups of a-z and 0-9 joined by single "
        + "underscores, and be neither pg nor start with pg_, which PostgreSQL reserves for its own roles";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_and_group_names_are_the_roles_the_module_connects_with()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var moduleConnectionProbe = new ModuleConnectionProbe(
            testModulesHost.Services.GetRequiredKeyedService<NpgsqlDataSource>(typeof(IPingsModule)));

        var loginName = ModuleLogins.GetLoginName(testModulesHost.Services, PingsDbContext.SchemaName);
        var groupName = ModuleLogins.GetGroupName(testModulesHost.Services);
        var sessionUser = await moduleConnectionProbe.ExecuteScalarAsync<String>("SELECT session_user");
        var groupMembers = await testModulesHost.Database.QueryAsync<String>(
            GroupMembersSql,
            new { Group = groupName });

        loginName.ShouldBe(sessionUser);
        groupMembers.ShouldContain(sessionUser);
    }

    [Fact]
    public void Login_and_group_names_follow_the_database_of_the_connection_string()
    {
        using var host = TestModulesHost.BuildUnstarted(WithDatabase("names_probe"));

        var loginName = ModuleLogins.GetLoginName(host.Services, PingsDbContext.SchemaName);
        var groupName = ModuleLogins.GetGroupName(host.Services);

        loginName.ShouldBe("names_probe__pings");
        groupName.ShouldBe("names_probe__modules");
    }

    [Theory]
    [InlineData("shop__eu")]
    [InlineData("_shop")]
    [InlineData("shop_")]
    [InlineData("Shop")]
    [InlineData("shop-eu")]
    [InlineData("pg")]
    [InlineData("pg_shop")]
    public async Task Database_name_that_breaks_the_rule_stops_the_start_before_it_connects(String databaseName)
    {
        using var host = TestModulesHost.BuildUnstarted(WithDatabase(databaseName));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => host.Services.PrepareModuleDatabasesAsync(CancellationToken));

        exception.Message.ShouldBe(
            $"The database name '{databaseName}' in ConnectionStrings:database {DatabaseNameRule}");
    }

    [Fact]
    public async Task Database_whose_group_would_pass_63_characters_stops_the_start_before_it_connects()
    {
        var databaseName = new String('d', 55);
        using var host = TestModulesHost.BuildUnstarted(WithDatabase(databaseName));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => host.Services.PrepareModuleDatabasesAsync(CancellationToken));

        exception.Message.ShouldBe(
            $"The database name '{databaseName}' makes the group role {databaseName}__modules 64 characters long, "
            + "more than the 63 PostgreSQL allows in a role name: shorten the database name to at most 54 characters");
    }

    [Fact]
    public async Task Database_of_54_characters_starts_with_a_group_of_63_characters()
    {
        var databaseName = $"boundary_{Guid.CreateVersion7():N}".PadRight(54, 'x');
        await new DatabaseProbe(postgresFixture.ConnectionString).ExecuteAsync($"CREATE DATABASE {databaseName}");

        await using var testModulesHost = await TestModulesHost.StartAsync(WithDatabase(databaseName));
        var groupName = ModuleLogins.GetGroupName(testModulesHost.Services);
        var groupMembers = await testModulesHost.Database.QueryAsync<String>(
            GroupMembersSql,
            new { Group = groupName });

        var pingId = await testModulesHost.CreatePingAsync("boundary");

        groupName.ShouldBe($"{databaseName}__modules");
        groupName.Length.ShouldBe(63);
        groupMembers.ShouldBe([$"{databaseName}__pings", $"{databaseName}__pongs"]);
        pingId.ShouldNotBe(Guid.Empty);
    }

    private String WithDatabase(String databaseName) =>
        new NpgsqlConnectionStringBuilder(postgresFixture.ConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;
}
