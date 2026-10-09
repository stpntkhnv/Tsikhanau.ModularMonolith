using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Features.GetPongSource;

namespace Tsikhanau.Monolith.IntegrationTests.Dispatching;

public sealed class CrossModuleQueryTests(PostgresFixture postgresFixture)
{
    private const String PongIdSql = "SELECT id FROM pongs.pongs WHERE ping_id = @PingId";

    private const String OverwritePongLabelSql = "UPDATE pongs.pongs SET label = 'pong copy' WHERE id = @PongId";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pong_source_carries_the_label_read_from_pings()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("source");
        await testModulesHost.ProcessQueuesAsync();
        var pongId = await testModulesHost.Database.ExecuteScalarAsync<Guid>(PongIdSql, new { PingId = pingId });
        await testModulesHost.Database.ExecuteAsync(OverwritePongLabelSql, new { PongId = pongId });

        var result = await testModulesHost.Pongs.ExecuteQueryAsync(new GetPongSourceQuery(pongId), CancellationToken);

        result.Value.ShouldBe(new PongSourceDto(pongId, "source"));
    }

    [Fact]
    public async Task Pong_source_of_an_unknown_pong_is_not_found()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);

        var result = await testModulesHost.Pongs.ExecuteQueryAsync(
            new GetPongSourceQuery(Guid.CreateVersion7()),
            CancellationToken);

        result.Error.ShouldBe(PongErrors.NotFound);
    }
}
