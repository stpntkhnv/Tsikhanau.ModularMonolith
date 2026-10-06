namespace ModularMonolith.Perf.Measuring;

internal sealed record LoadResult(
    String Name,
    Int32 Concurrency,
    Int32 Operations,
    LatencySummary Latency,
    ResourceUsage Usage)
{
    public Double Seconds => Usage.Elapsed.TotalSeconds;

    private Double Throughput => Operations / Seconds;

    private Double AllocatedBytesPerOperation => Usage.AllocatedBytes / (Double)Operations;

    private Double ProcessorMicrosecondsPerOperation => Usage.ProcessorTime.TotalMicroseconds / Operations;

    public override String ToString() =>
        $"{Name} c={Concurrency} n={Operations}: {Throughput:F0} ops/s, "
            + $"p50 {Latency.P50.TotalMilliseconds:F3} ms, p95 {Latency.P95.TotalMilliseconds:F3} ms, "
            + $"p99 {Latency.P99.TotalMilliseconds:F3} ms, mean {Latency.Mean.TotalMilliseconds:F3} ms, "
            + $"alloc {AllocatedBytesPerOperation:F0} B/op, cpu {ProcessorMicrosecondsPerOperation:F0} us/op, "
            + $"gc0/1/2 {Usage.Gen0Collections}/{Usage.Gen1Collections}/{Usage.Gen2Collections}, "
            + $"load {MachineLoad.Read()}";
}
