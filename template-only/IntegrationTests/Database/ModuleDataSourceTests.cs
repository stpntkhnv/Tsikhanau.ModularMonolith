using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Testing.Database;
using Npgsql;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings;

namespace Tsikhanau.Monolith.IntegrationTests.Database;

public sealed class ModuleDataSourceTests(PostgresFixture postgresFixture)
{
    [Fact]
    public void Module_commands_may_run_as_long_as_a_queue_handler()
    {
        using var host = TestModulesHost.BuildUnstarted(postgresFixture.ConnectionString);
        var dataSource = host.Services.GetRequiredKeyedService<NpgsqlDataSource>(typeof(IPingsModule));

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);

        connectionStringBuilder.CommandTimeout.ShouldBe((Int32)TestModulesHost.HandlerTimeout.TotalSeconds);
    }
}
