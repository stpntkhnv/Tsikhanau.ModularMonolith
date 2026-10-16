using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class GiveBackRefusal : IAsyncDisposable
{
    private readonly DatabaseProbe _databaseProbe;
    private readonly String _schema;
    private readonly String _table;

    private GiveBackRefusal(DatabaseProbe databaseProbe, String schema, String table)
    {
        _databaseProbe = databaseProbe;
        _schema = schema;
        _table = table;
    }

    public static async Task<GiveBackRefusal> InstallAsync(
        DatabaseProbe databaseProbe,
        String schema,
        String table,
        String sqlState)
    {
        await databaseProbe.ExecuteAsync(BuildInstallSql(schema, table, sqlState));
        return new GiveBackRefusal(databaseProbe, schema, table);
    }

    public async ValueTask DisposeAsync() => await _databaseProbe.ExecuteAsync(BuildDropSql());

    private static String BuildInstallSql(String schema, String table, String sqlState) =>
        $$"""
        CREATE FUNCTION {{schema}}.refuse_give_back() RETURNS trigger LANGUAGE plpgsql AS $body$
        BEGIN
            IF NEW.attempts < OLD.attempts THEN
                RAISE EXCEPTION 'give-back refused' USING ERRCODE = '{{sqlState}}';
            END IF;
            RETURN NEW;
        END
        $body$;
        CREATE TRIGGER refuse_give_back BEFORE UPDATE ON {{schema}}.{{table}}
            FOR EACH ROW EXECUTE FUNCTION {{schema}}.refuse_give_back();
        """;

    private String BuildDropSql() =>
        $"""
        DROP TRIGGER refuse_give_back ON {_schema}.{_table};
        DROP FUNCTION {_schema}.refuse_give_back();
        """;
}
