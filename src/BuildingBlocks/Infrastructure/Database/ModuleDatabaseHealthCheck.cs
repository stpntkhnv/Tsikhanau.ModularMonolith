using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModularMonolith.Shared.Time;
using Npgsql;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Database;

internal sealed class ModuleDatabaseHealthCheck(NpgsqlDataSource npgsqlDataSource) : IHealthCheck
{
    public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(2);

    private static readonly Task<HealthCheckResult> NoProbeYet = Task.FromResult(HealthCheckResult.Healthy());

    private readonly Lock _probeLock = new();

    private Task<HealthCheckResult> _probe = NoProbeYet;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        GetOrStartProbe()
            .WaitAsync(cancellationToken);

    private Task<HealthCheckResult> GetOrStartProbe()
    {
        lock (_probeLock)
        {
            if (_probe.IsCompleted)
            {
                _probe = ProbeAsync();
            }

            return _probe;
        }
    }

    private async Task<HealthCheckResult> ProbeAsync()
    {
        try
        {
            await SelectOneAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy(exception: exception);
        }
    }

    private async Task SelectOneAsync()
    {
        await using var command = npgsqlDataSource.CreateCommand("SELECT 1");
        command.CommandTimeout = CheckTimeout.TotalSecondsRoundedUp;
        await command.ExecuteScalarAsync(CancellationToken.None);
    }
}
