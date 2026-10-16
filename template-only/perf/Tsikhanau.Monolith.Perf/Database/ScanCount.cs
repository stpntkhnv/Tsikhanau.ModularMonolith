namespace Tsikhanau.Monolith.Perf.Database;

internal sealed record ScanCount(String TableName, String IndexName, Int64 Scans);
