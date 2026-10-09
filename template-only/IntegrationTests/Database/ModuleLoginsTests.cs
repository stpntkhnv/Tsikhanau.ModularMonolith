using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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
        """;

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
    public void Login_and_group_names_follow_the_configured_login_prefix()
    {
        using var host = TestModulesHost.BuildUnstarted(
            postgresFixture.ConnectionString,
            builder => builder.Configuration["App:Database:LoginPrefix"] = "prefix_probe");

        var loginName = ModuleLogins.GetLoginName(host.Services, PingsDbContext.SchemaName);
        var groupName = ModuleLogins.GetGroupName(host.Services);

        loginName.ShouldBe("prefix_probe_pings");
        groupName.ShouldBe("prefix_probe_modules");
    }
}
