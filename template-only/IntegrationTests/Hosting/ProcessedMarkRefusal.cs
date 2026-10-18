using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class ProcessedMarkRefusal : IAsyncDisposable
{
    private readonly DatabaseProbe _databaseProbe;
    private readonly String _schema;
    private readonly String _table;

    private ProcessedMarkRefusal(DatabaseProbe databaseProbe, String schema, String table)
    {
        _databaseProbe = databaseProbe;
        _schema = schema;
        _table = table;
    }

    public static async Task<ProcessedMarkRefusal> InstallAsync(
        DatabaseProbe databaseProbe,
        String schema,
        String table)
    {
        await databaseProbe.ExecuteAsync(BuildInstallSql(schema, table));
        return new ProcessedMarkRefusal(databaseProbe, schema, table);
    }

    public async ValueTask DisposeAsync() => await _databaseProbe.ExecuteAsync(BuildDropSql());

    private static String BuildInstallSql(String schema, String table) =>
        $$"""
        CREATE FUNCTION {{schema}}.refuse_processed_mark() RETURNS trigger LANGUAGE plpgsql AS $body$
        BEGIN
            RAISE EXCEPTION 'processed mark refused' USING ERRCODE = 'check_violation';
        END
        $body$;
        CREATE CONSTRAINT TRIGGER refuse_processed_mark AFTER UPDATE ON {{schema}}.{{table}}
            DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW WHEN (NEW.processed_on IS NOT NULL)
            EXECUTE FUNCTION {{schema}}.refuse_processed_mark();
        """;

    private String BuildDropSql() =>
        $"""
        DROP TRIGGER refuse_processed_mark ON {_schema}.{_table};
        DROP FUNCTION {_schema}.refuse_processed_mark();
        """;
}
