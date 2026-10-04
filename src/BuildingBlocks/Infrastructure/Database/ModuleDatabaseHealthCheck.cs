using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Database;

internal sealed class ModuleDatabaseHealthCheck(NpgsqlDataSource npgsqlDataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = npgsqlDataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy(exception: exception);
        }
    }
}
