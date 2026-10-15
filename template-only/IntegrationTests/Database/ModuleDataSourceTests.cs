using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public void Module_connects_under_the_names_of_the_database()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(postgresFixture.ConnectionString)
        {
            Database = "names_probe",
        }.ConnectionString;

        using var host = TestModulesHost.BuildUnstarted(connectionString);
        var dataSource = host.Services.GetRequiredKeyedService<NpgsqlDataSource>(typeof(IPingsModule));

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);

        connectionStringBuilder.Database.ShouldBe("names_probe");
        connectionStringBuilder.Username.ShouldBe("names_probe__pings");
        connectionStringBuilder.ApplicationName.ShouldBe("names_probe-pings");
    }
}
