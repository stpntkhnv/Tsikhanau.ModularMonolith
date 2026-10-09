namespace Tsikhanau.Monolith.Perf.Database;

internal sealed record QueueTableSample(
    Int64 LiveRows,
    Int64 DeadRows,
    Int64 Autovacuums,
    Int64 IndexBytes,
    Int64 PendingRows,
    Int64 Claims,
    Double ClaimMilliseconds,
    Int64 ClaimedRows);
