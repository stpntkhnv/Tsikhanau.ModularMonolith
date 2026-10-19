using Npgsql;
using Tsikhanau.Monolith.Testing.Database;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class RowChangeRefusal : IAsyncDisposable
{
    public const String Message = "row change refused";

    public const String AnyRow = "true";

    public const String GiveBack = "NEW.attempts < OLD.attempts";

    public const String ProcessedMark = "NEW.processed_on IS NOT NULL";

    private readonly DatabaseProbe _databaseProbe;
    private readonly String _schema;
    private readonly String _table;

    private RowChangeRefusal(DatabaseProbe databaseProbe, String schema, String table)
    {
        _databaseProbe = databaseProbe;
        _schema = schema;
        _table = table;
    }

    public static async Task<RowChangeRefusal> InstallAsync(
        DatabaseProbe databaseProbe,
        String schema,
        String table,
        String triggerEvent,
        String condition,
        String sqlState = PostgresErrorCodes.CheckViolation)
    {
        await databaseProbe.ExecuteAsync(BuildInstallSql(schema, table, triggerEvent, condition, sqlState));
        return new RowChangeRefusal(databaseProbe, schema, table);
    }

    public async ValueTask DisposeAsync() => await _databaseProbe.ExecuteAsync(BuildDropSql());

    private static String BuildInstallSql(
        String schema,
        String table,
        String triggerEvent,
        String condition,
        String sqlState) =>
        $$"""
        CREATE FUNCTION {{schema}}.refuse_row_change() RETURNS trigger LANGUAGE plpgsql AS $body$
        BEGIN
            RAISE EXCEPTION '{{Message}}' USING ERRCODE = '{{sqlState}}';
        END
        $body$;
        CREATE CONSTRAINT TRIGGER refuse_row_change AFTER {{triggerEvent}} ON {{schema}}.{{table}}
            DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW WHEN ({{condition}})
            EXECUTE FUNCTION {{schema}}.refuse_row_change();
        """;

    private String BuildDropSql() =>
        $"""
        DROP TRIGGER refuse_row_change ON {_schema}.{_table};
        DROP FUNCTION {_schema}.refuse_row_change();
        """;
}
