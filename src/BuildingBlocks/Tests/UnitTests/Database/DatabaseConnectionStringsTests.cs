using Microsoft.Extensions.Configuration;
using ModularMonolith.BuildingBlocks.Infrastructure.Database;
using Npgsql;

namespace ModularMonolith.BuildingBlocks.UnitTests.Database;

public sealed class DatabaseConnectionStringsTests
{
    [Fact]
    public void Missing_connection_string_is_refused()
    {
        var exception = Should.Throw<InvalidOperationException>(
            () => DatabaseConnectionStrings.From(CreateConfiguration(null)));

        exception.Message.ShouldContain("Connection string 'modularmonolith' is missing");
    }

    [Fact]
    public void Connection_string_without_a_password_is_refused()
    {
        var configuration = CreateConfiguration("Host=db;Database=modularmonolith;Username=postgres");

        var exception = Should.Throw<InvalidOperationException>(() => DatabaseConnectionStrings.From(configuration));

        exception.Message.ShouldContain("password");
    }

    [Fact]
    public void Module_connection_keeps_the_server_and_swaps_the_identity()
    {
        var connectionStrings = DatabaseConnectionStrings.From(
            CreateConfiguration("Host=db;Port=5433;Database=modularmonolith;Username=postgres;Password=p"));

        var login = connectionStrings.CreateModuleLogin("pings");

        var moduleConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForModule(login, "modularmonolith-pings", 7));

        moduleConnectionStringBuilder.Host.ShouldBe("db");
        moduleConnectionStringBuilder.Port.ShouldBe(5433);
        moduleConnectionStringBuilder.Database.ShouldBe("modularmonolith");
        moduleConnectionStringBuilder.Username.ShouldBe("modularmonolith_pings");
        moduleConnectionStringBuilder.Password.ShouldBe(login.Password);
        moduleConnectionStringBuilder.ApplicationName.ShouldBe("modularmonolith-pings");
        moduleConnectionStringBuilder.MaxPoolSize.ShouldBe(7);
        moduleConnectionStringBuilder.Pooling.ShouldBeTrue();
    }

    [Fact]
    public void Admin_connection_is_never_pooled_and_module_connections_always_are()
    {
        var connectionStrings = DatabaseConnectionStrings.From(
            CreateConfiguration("Host=db;Database=modularmonolith;Username=postgres;Password=p;Pooling=false"));

        var login = connectionStrings.CreateModuleLogin("pings");

        var adminConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionStrings.Admin);
        var moduleConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForModule(login, "modularmonolith-pings", 7));

        adminConnectionStringBuilder.Pooling.ShouldBeFalse();
        moduleConnectionStringBuilder.Pooling.ShouldBeTrue();
    }

    [Fact]
    public void Connection_strings_never_persist_security_info()
    {
        var connectionStrings = DatabaseConnectionStrings.From(
            CreateConfiguration(
                "Host=db;Database=modularmonolith;Username=postgres;Password=p;Persist Security Info=true"));

        var login = connectionStrings.CreateModuleLogin("pings");

        var adminConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionStrings.Admin);
        var moduleConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForModule(login, "modularmonolith-pings", 7));

        adminConnectionStringBuilder.PersistSecurityInfo.ShouldBeFalse();
        moduleConnectionStringBuilder.PersistSecurityInfo.ShouldBeFalse();
    }

    [Fact]
    public void Admin_password_with_separators_survives()
    {
        var configuredConnectionStringBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = "db",
            Database = "modularmonolith",
            Username = "postgres",
            Password = "a;b'c=d",
        };

        var connectionStrings = DatabaseConnectionStrings.From(
            CreateConfiguration(configuredConnectionStringBuilder.ConnectionString));

        var adminConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionStrings.Admin);
        var login = connectionStrings.CreateModuleLogin("pings");

        adminConnectionStringBuilder.Password.ShouldBe("a;b'c=d");
        login.Password.ShouldBe(ModuleLogin.Create("pings", "a;b'c=d").Password);
    }

    private static IConfiguration CreateConfiguration(String? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<String, String?> { ["ConnectionStrings:modularmonolith"] = connectionString })
            .Build();
}
