using Testcontainers.PostgreSql;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public String ConnectionString => _postgreSqlContainer.GetConnectionString();

    public async ValueTask InitializeAsync() =>
        await _postgreSqlContainer.StartAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync() => await _postgreSqlContainer.DisposeAsync();
}
