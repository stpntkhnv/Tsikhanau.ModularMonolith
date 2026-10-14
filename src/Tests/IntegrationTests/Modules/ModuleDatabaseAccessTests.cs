using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Testing.App;
using Npgsql;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.Testing.Modules;

namespace ModularMonolith.IntegrationTests.Modules;

public sealed class ModuleDatabaseAccessTests(AppFixture appFixture) : AppTest(appFixture)
{
    private const String DeniedSqlState = PostgresErrorCodes.InsufficientPrivilege;

    private const String SuperuserSql = "SELECT rolsuper FROM pg_roles WHERE rolname = current_user";

    private const String OwnerSql = "SELECT pg_get_userbyid(nspowner) FROM pg_namespace WHERE nspname = @Schema";

    private const String ObjectsOwnedByOthersSql = """
        SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = @Schema AND pg_get_userbyid(c.relowner) <> @Login
        """;

    [Theory]
    [EveryProductionModule]
    public async Task Module_connects_as_its_own_login(String name)
    {
        var moduleConnectionProbe = CreateConnectionProbe(name);
        var loginName = App.GetModuleLoginName(name);

        var sessionUser = await moduleConnectionProbe.ExecuteScalarAsync<String>("SELECT session_user");
        var currentUser = await moduleConnectionProbe.ExecuteScalarAsync<String>("SELECT current_user");
        var isSuperuser = await moduleConnectionProbe.ExecuteScalarAsync<Boolean>(SuperuserSql);

        sessionUser.ShouldBe(loginName);
        currentUser.ShouldBe(loginName);
        isSuperuser.ShouldBeFalse();
    }

    [Theory]
    [EveryProductionModulePair]
    public async Task Module_cannot_become_another_module(String name, String otherName)
    {
        var moduleConnectionProbe = CreateConnectionProbe(name);
        var otherSchema = App.GetModule(otherName).Schema;
        var otherLoginName = App.GetModuleLoginName(otherName);
        var outboxAfterResetSql = $"RESET ROLE; SELECT count(*) FROM \"{otherSchema}\".outbox_messages";

        var setRoleSqlState = await moduleConnectionProbe.ExecuteAndFindSqlStateAsync($"SET ROLE {otherLoginName}");
        var afterResetSqlState = await moduleConnectionProbe.ExecuteAndFindSqlStateAsync(outboxAfterResetSql);
        var groupRoleSqlState = await moduleConnectionProbe.ExecuteAndFindSqlStateAsync(
            $"SET ROLE {App.ModuleGroupName}");

        setRoleSqlState.ShouldBe(DeniedSqlState);
        afterResetSqlState.ShouldBe(DeniedSqlState);
        groupRoleSqlState.ShouldBe(DeniedSqlState);
    }

    [Theory]
    [EveryProductionModulePair]
    public async Task Module_reaches_another_module_only_through_its_inbox(String name, String otherName)
    {
        var moduleConnectionProbe = CreateConnectionProbe(name);
        var ownSchema = App.GetModule(name).Schema;
        var foreignSchema = App.GetModule(otherName).Schema;

        var ownReadSqlState = await moduleConnectionProbe.ExecuteAndFindSqlStateAsync(
            $"SELECT count(*) FROM \"{ownSchema}\".outbox_messages");

        var ownWriteSqlState = await moduleConnectionProbe.ExecuteAndFindSqlStateAsync(
            BuildInboxInsertSql(ownSchema, Guid.CreateVersion7()));

        var outboxSqlState = await moduleConnectionProbe.ExecuteAndFindSqlStateAsync(
            $"SELECT count(*) FROM \"{foreignSchema}\".outbox_messages");

        var commandsSqlState = await moduleConnectionProbe.ExecuteAndFindSqlStateAsync(
            $"SELECT count(*) FROM \"{foreignSchema}\".internal_commands");

        var inboxIdsSqlState = await moduleConnectionProbe.ExecuteAndFindSqlStateAsync(
            $"SELECT id FROM \"{foreignSchema}\".inbox_messages");

        var inboxPayloadsSqlState = await moduleConnectionProbe.ExecuteAndFindSqlStateAsync(
            $"SELECT payload FROM \"{foreignSchema}\".inbox_messages");

        var inboxRowsSqlState = await moduleConnectionProbe.ExecuteAndFindSqlStateAsync(
            $"SELECT * FROM \"{foreignSchema}\".inbox_messages");

        var deliverySqlState = await moduleConnectionProbe.ExecuteAndFindSqlStateAsync(
            BuildInboxInsertSql(foreignSchema, Guid.CreateVersion7()));

        ownReadSqlState.HasValue.ShouldBeFalse();
        ownWriteSqlState.HasValue.ShouldBeFalse();
        outboxSqlState.ShouldBe(DeniedSqlState);
        commandsSqlState.ShouldBe(DeniedSqlState);
        inboxIdsSqlState.HasValue.ShouldBeFalse();
        inboxPayloadsSqlState.ShouldBe(DeniedSqlState);
        inboxRowsSqlState.ShouldBe(DeniedSqlState);
        deliverySqlState.HasValue.ShouldBeFalse();
    }

    [Theory]
    [EveryProductionModule]
    public async Task Module_login_owns_its_schema_and_everything_in_it(String name)
    {
        var schema = App.GetModule(name).Schema;
        var loginName = App.GetModuleLoginName(name);

        var ownerName = await App.Database.ExecuteScalarAsync<String>(OwnerSql, new { Schema = schema });
        var objectsOwnedByOthers = await App.Database.ExecuteScalarAsync<Int64>(
            ObjectsOwnedByOthersSql,
            new { Schema = schema, Login = loginName });

        ownerName.ShouldBe(loginName);
        objectsOwnedByOthers.ShouldBe(0);
    }

    private static String BuildInboxInsertSql(String schema, Guid messageId) =>
        $$"""
        INSERT INTO "{{schema}}".inbox_messages
            (id, type, payload, trace_parent, attempts, next_attempt_on, occurred_on, received_on)
        VALUES ('{{messageId}}', 'probe', '{}'::jsonb, NULL, 0, now(), now(), now())
        ON CONFLICT (id) DO NOTHING
        """;

    private ModuleConnectionProbe CreateConnectionProbe(String name) =>
        new(App.Services.GetRequiredKeyedService<NpgsqlDataSource>(App.GetModule(name).Key));
}
