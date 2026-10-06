namespace ModularMonolith.Perf.Hosting;

internal sealed record PerfHostSettings(
    Boolean RunInBackground,
    Int32 MaxConnections,
    TimeSpan PollInterval,
    Boolean LogsToConsole);
