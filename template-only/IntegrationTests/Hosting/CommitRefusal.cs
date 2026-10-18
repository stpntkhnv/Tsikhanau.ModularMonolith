using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class CommitRefusal : IAsyncDisposable
{
    public const String Message = "commit refused";

    private readonly DatabaseProbe _databaseProbe;
    private readonly String _schema;
    private readonly String _table;

    private CommitRefusal(DatabaseProbe databaseProbe, String schema, String table)
    {
        _databaseProbe = databaseProbe;
        _schema = schema;
        _table = table;
    }

    public static async Task<CommitRefusal> InstallAsync(DatabaseProbe databaseProbe, String schema, String table)
    {
        await databaseProbe.ExecuteAsync(BuildInstallSql(schema, table));
        return new CommitRefusal(databaseProbe, schema, table);
    }

    public async ValueTask DisposeAsync() => await _databaseProbe.ExecuteAsync(BuildDropSql());

    private static String BuildInstallSql(String schema, String table) =>
        $$"""
        CREATE FUNCTION {{schema}}.refuse_commit() RETURNS trigger LANGUAGE plpgsql AS $body$
        BEGIN
            RAISE EXCEPTION '{{Message}}' USING ERRCODE = 'check_violation';
        END
        $body$;
        CREATE CONSTRAINT TRIGGER refuse_commit AFTER INSERT ON {{schema}}.{{table}}
            DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION {{schema}}.refuse_commit();
        """;

    private String BuildDropSql() =>
        $"""
        DROP TRIGGER refuse_commit ON {_schema}.{_table};
        DROP FUNCTION {_schema}.refuse_commit();
        """;
}
