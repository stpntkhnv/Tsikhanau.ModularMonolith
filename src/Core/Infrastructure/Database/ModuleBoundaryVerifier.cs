using Dapper;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Inbox;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal static class ModuleBoundaryVerifier
{
    private const String ForbiddenPrivilegesSql = """
        WITH modules AS (
            SELECT login::name AS login, schema FROM unnest(@Logins::text[], @Schemas::text[]) AS m(login, schema)
        ),
        pairs AS (
            SELECT l.login, s.schema FROM modules l JOIN modules s ON s.schema <> l.schema
        ),
        relations AS (
            SELECT p.login, n.nspname, c.oid, c.relname, c.relkind
            FROM pairs p
            JOIN pg_namespace n ON n.nspname = p.schema
            JOIN pg_class c ON c.relnamespace = n.oid
        ),
        table_privileges AS (
            SELECT unnest(
                ARRAY['SELECT', 'INSERT', 'UPDATE', 'DELETE', 'TRUNCATE', 'REFERENCES', 'TRIGGER']
                || CASE WHEN current_setting('server_version_num')::int >= 170000
                        THEN ARRAY['MAINTAIN'] ELSE ARRAY[]::text[] END) AS privilege
        )
        SELECT m.login AS "Login", 'CREATE' AS "Privilege", format('DATABASE %I', current_database()) AS "Target"
        FROM modules m
        WHERE has_database_privilege(m.login, current_database(), 'CREATE')
        UNION ALL
        SELECT p.login, 'CREATE', format('SCHEMA %I', p.schema)
        FROM pairs p
        WHERE has_schema_privilege(p.login, p.schema, 'CREATE')
        UNION ALL
        SELECT r.login, t.privilege, format('%I.%I', r.nspname, r.relname)
        FROM relations r CROSS JOIN table_privileges t
        WHERE r.relkind IN ('r', 'p', 'v', 'm', 'f')
          AND has_table_privilege(r.login, r.oid, t.privilege)
          AND NOT (r.relname = @Inbox AND t.privilege = 'INSERT')
        UNION ALL
        SELECT r.login, format('%s (%I)', k.privilege, a.attname), format('%I.%I', r.nspname, r.relname)
        FROM relations r
        JOIN pg_attribute a ON a.attrelid = r.oid AND a.attnum > 0 AND NOT a.attisdropped
        CROSS JOIN unnest(ARRAY['SELECT', 'INSERT', 'UPDATE', 'REFERENCES']) AS k(privilege)
        WHERE r.relkind IN ('r', 'p', 'v', 'm', 'f')
          AND has_column_privilege(r.login, r.oid, a.attnum, k.privilege)
          AND NOT has_table_privilege(r.login, r.oid, k.privilege)
          AND NOT (r.relname = @Inbox AND (k.privilege = 'INSERT' OR (k.privilege = 'SELECT' AND a.attname = 'id')))
        UNION ALL
        SELECT r.login, q.privilege, format('SEQUENCE %I.%I', r.nspname, r.relname)
        FROM relations r CROSS JOIN unnest(ARRAY['USAGE', 'SELECT', 'UPDATE']) AS q(privilege)
        WHERE r.relkind = 'S' AND has_sequence_privilege(r.login, r.oid, q.privilege)
        ORDER BY 1, 3, 2
        """;

    private const String Problem =
        "Module logins must reach other modules only through INSERT and SELECT (id) on their inboxes";

    public static async Task VerifyAsync(
        NpgsqlConnection adminNpgsqlConnection,
        IReadOnlyList<ModuleLogin> logins,
        CancellationToken cancellationToken)
    {
        var forbiddenPrivileges = await ReadForbiddenPrivilegesAsync(adminNpgsqlConnection, logins, cancellationToken);
        if (forbiddenPrivileges.Count > 0)
        {
            throw new InvalidOperationException(
                $"{Problem}. Revoke the forbidden privileges: {String.Join("; ", forbiddenPrivileges)}");
        }
    }

    private static async Task<IReadOnlyList<ForbiddenPrivilege>> ReadForbiddenPrivilegesAsync(
        NpgsqlConnection adminNpgsqlConnection,
        IReadOnlyList<ModuleLogin> logins,
        CancellationToken cancellationToken)
    {
        var parameters = new
        {
            Logins = logins
                .Select(login => login.Name)
                .ToArray(),
            Schemas = logins
                .Select(login => login.Schema)
                .ToArray(),
            Inbox = InboxMessage.Table,
        };

        var forbiddenPrivileges = await adminNpgsqlConnection.QueryAsync<ForbiddenPrivilege>(
            new CommandDefinition(ForbiddenPrivilegesSql, parameters, cancellationToken: cancellationToken));

        return forbiddenPrivileges.AsList();
    }
}
