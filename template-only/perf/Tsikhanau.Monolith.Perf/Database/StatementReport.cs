using Tsikhanau.Monolith.Shared.Collections;

namespace Tsikhanau.Monolith.Perf.Database;

internal static class StatementReport
{
    public static async Task WriteAsync(PerfDatabase database, Int32 operations, CancellationToken cancellationToken)
    {
        var statements = await database.ReadStatementsAsync(cancellationToken);
        Console.WriteLine($"  statements per op (pg_stat_statements, {operations} ops):");
        statements
            .Where(statement => IsFrequent(statement, operations))
            .Select(statement => BuildLine(statement, operations))
            .ForEach(Console.WriteLine);
    }

    private static Boolean IsFrequent(StatementStatistics statement, Int32 operations) =>
        statement.Calls >= operations / 10;

    private static String BuildLine(StatementStatistics statement, Int32 operations) =>
        $"    {statement.Calls / (Double)operations,5:F2} x  mean {statement.MeanMilliseconds * 1000,7:F1} us  "
            + $"[{statement.Role}] {statement.Query}";
}
