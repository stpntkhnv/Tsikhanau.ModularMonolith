using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Testing.Database;
using Npgsql;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings;

namespace Tsikhanau.Monolith.IntegrationTests.Database;

public sealed class CrossModuleAccessTests(PostgresFixture postgresFixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pings_query_cannot_read_the_pongs_schema()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var sqlConnectionFactory = testModulesHost.Services.GetRequiredService<ISqlConnectionFactory<IPingsModule>>();
        await using var connection = await sqlConnectionFactory.OpenConnectionAsync(CancellationToken);
        var countCommand = new CommandDefinition(
            "SELECT count(*) FROM pongs.pongs",
            cancellationToken: CancellationToken);

        var exception = await Should.ThrowAsync<PostgresException>(
            () => connection.ExecuteScalarAsync<Int64>(countCommand));

        exception.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }
}
