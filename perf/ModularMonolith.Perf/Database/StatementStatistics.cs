namespace ModularMonolith.Perf.Database;

internal sealed record StatementStatistics(Int64 Calls, Double MeanMilliseconds, String Query, String Role);
