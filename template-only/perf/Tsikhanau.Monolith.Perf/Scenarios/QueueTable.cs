using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.Perf.Scenarios;

internal sealed record QueueTable(String Schema, String Table)
{
    private const String SeededOn = "seeded_on";

    public static IReadOnlyList<QueueTable> Watched { get; } =
        [new(PingsDbContext.SchemaName, OutboxMessage.Table), new(PongsDbContext.SchemaName, InboxMessage.Table)];

    public static IReadOnlyList<QueueTable> All { get; } =
        [.. GetTables(PingsDbContext.SchemaName), .. GetTables(PongsDbContext.SchemaName)];

    public String Name => $"{Schema}.{Table}";

    private String QualifiedName => $"\"{Schema}\".{Table}";

    private Boolean HasReceivedOn => Table == InboxMessage.Table;

    public String BuildSeedSql(Int32 rows) =>
        $"""
        INSERT INTO {QualifiedName} (id, type, payload, attempts, next_attempt_on, processed_on, {BuildTimeColumns()})
        SELECT gen_random_uuid(), 'seeded', '{"{}"}', 1, {SeededOn}, {SeededOn}, {BuildTimeValues()}
        FROM generate_series(1, {rows}), (SELECT now() - interval '1 day' AS {SeededOn}) AS seed
        """;

    public String BuildVacuumSql() => $"VACUUM (ANALYZE) {QualifiedName}";

    public String BuildReadSettingsSql() =>
        $"SELECT coalesce(array_to_string(reloptions, ', '), '') FROM pg_class WHERE oid = '{QualifiedName}'::regclass";

    public String BuildSetSettingsSql(String settings) => $"ALTER TABLE {QualifiedName} SET ({settings})";

    public String BuildSampleSql() =>
        $"""
        SELECT t.n_live_tup AS LiveRows, t.n_dead_tup AS DeadRows, t.autovacuum_count AS Autovacuums,
               pg_indexes_size(t.relid) AS IndexBytes,
               (SELECT count(*) FROM {QualifiedName} WHERE processed_on IS NULL AND failed_on IS NULL) AS PendingRows,
               claims.calls AS Claims, claims.milliseconds AS ClaimMilliseconds, claims.rows AS ClaimedRows
        FROM pg_stat_user_tables t,
             (SELECT coalesce(sum(s.calls), 0)::bigint AS calls,
                     coalesce(sum(s.total_exec_time), 0)::float8 AS milliseconds,
                     coalesce(sum(s.rows), 0)::bigint AS rows
              FROM pg_stat_statements s
              JOIN pg_database d ON d.oid = s.dbid
              WHERE d.datname = current_database()
                AND s.query LIKE 'WITH candidates AS%'
                AND s.query LIKE '%{QualifiedName}%') AS claims
        WHERE t.schemaname = '{Schema}' AND t.relname = '{Table}'
        """;

    private static IEnumerable<QueueTable> GetTables(String schema) =>
        [new(schema, OutboxMessage.Table), new(schema, InboxMessage.Table), new(schema, InternalCommandMessage.Table)];

    private String BuildTimeColumns() => HasReceivedOn ? "occurred_on, received_on" : "occurred_on";

    private String BuildTimeValues() => HasReceivedOn ? $"{SeededOn}, {SeededOn}" : SeededOn;
}
