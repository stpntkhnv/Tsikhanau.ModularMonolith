using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.UnitTests.Database;

public sealed class DatabaseConnectionStringsTests
{
    private const String ServerConnectionString = "Host=db;Database=monolith;Username=postgres;Password=p";

    private static readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(10);

    private static readonly IHostEnvironment Development = new TestHostEnvironment(Environments.Development);

    private static readonly IHostEnvironment Production = new TestHostEnvironment(Environments.Production);

    private static readonly ModuleLoginNames LoginNames = new("monolith");

    [Fact]
    public void Missing_connection_string_is_refused()
    {
        var exception = Should.Throw<InvalidOperationException>(
            () => DatabaseConnectionStrings.From(CreateConfiguration(null), Development));

        exception.Message.ShouldContain("Connection string 'database' is missing");
    }

    [Fact]
    public void Connection_string_without_a_password_is_refused()
    {
        var configuration = CreateConfiguration("Host=db;Database=monolith;Username=postgres");

        var exception = Should.Throw<InvalidOperationException>(
            () => DatabaseConnectionStrings.From(configuration, Development));

        exception.Message.ShouldContain("password");
    }

    [Fact]
    public void Module_connection_keeps_the_server_and_swaps_the_identity()
    {
        var connectionStrings = DatabaseConnectionStrings.From(
            CreateConfiguration("Host=db;Port=5433;Database=monolith;Username=postgres;Password=p"),
            Development);

        var login = ModuleLogin.Create(LoginNames, "pings", TestSettings.LoginKey);

        var moduleConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForModule(login, "monolith-pings", 7, TimeSpan.FromMinutes(1)));

        moduleConnectionStringBuilder.Host.ShouldBe("db");
        moduleConnectionStringBuilder.Port.ShouldBe(5433);
        moduleConnectionStringBuilder.Database.ShouldBe("monolith");
        moduleConnectionStringBuilder.Username.ShouldBe("monolith_pings");
        moduleConnectionStringBuilder.Password.ShouldBe(login.Password);
        moduleConnectionStringBuilder.ApplicationName.ShouldBe("monolith-pings");
        moduleConnectionStringBuilder.MaxPoolSize.ShouldBe(7);
        moduleConnectionStringBuilder.Pooling.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1.0, 30)]
    [InlineData(60.0, 60)]
    [InlineData(90.5, 91)]
    public void Module_command_timeout_covers_the_handler_timeout_and_never_drops_below_the_driver_default(
        Double handlerTimeoutSeconds,
        Int32 commandTimeoutSeconds)
    {
        var connectionStrings = DatabaseConnectionStrings.From(
            CreateConfiguration("Host=db;Database=monolith;Username=postgres;Password=p"),
            Development);

        var login = ModuleLogin.Create(LoginNames, "pings", TestSettings.LoginKey);
        var handlerTimeout = TimeSpan.FromSeconds(handlerTimeoutSeconds);

        var moduleConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForModule(login, "monolith-pings", 7, handlerTimeout));

        moduleConnectionStringBuilder.CommandTimeout.ShouldBe(commandTimeoutSeconds);
    }

    [Fact]
    public void Admin_command_timeout_covers_the_prepare_timeout()
    {
        var connectionStrings = DatabaseConnectionStrings.From(
            CreateConfiguration("Host=db;Database=monolith;Username=postgres;Password=p;Command Timeout=1"),
            Development);

        var adminConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForAdmin(TimeSpan.FromSeconds(90.5)));

        adminConnectionStringBuilder.CommandTimeout.ShouldBe(91);
    }

    [Fact]
    public void Module_connection_prepares_its_frequent_statements()
    {
        var connectionStrings = DatabaseConnectionStrings.From(
            CreateConfiguration("Host=db;Database=monolith;Username=postgres;Password=p"),
            Development);

        var login = ModuleLogin.Create(LoginNames, "pings", TestSettings.LoginKey);

        var moduleConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForModule(login, "monolith-pings", 7, TimeSpan.FromMinutes(1)));

        moduleConnectionStringBuilder.MaxAutoPrepare.ShouldBe(20);
    }

    [Fact]
    public void Admin_connection_is_never_pooled_and_module_connections_always_are()
    {
        var connectionStrings = DatabaseConnectionStrings.From(
            CreateConfiguration("Host=db;Database=monolith;Username=postgres;Password=p;Pooling=false"),
            Development);

        var login = ModuleLogin.Create(LoginNames, "pings", TestSettings.LoginKey);

        var adminConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForAdmin(PrepareTimeout));

        var moduleConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForModule(login, "monolith-pings", 7, TimeSpan.FromMinutes(1)));

        adminConnectionStringBuilder.Pooling.ShouldBeFalse();
        moduleConnectionStringBuilder.Pooling.ShouldBeTrue();
    }

    [Fact]
    public void Connection_strings_never_persist_security_info()
    {
        var connectionStrings = DatabaseConnectionStrings.From(
            CreateConfiguration(
                "Host=db;Database=monolith;Username=postgres;Password=p;Persist Security Info=true"),
            Development);

        var login = ModuleLogin.Create(LoginNames, "pings", TestSettings.LoginKey);

        var adminConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForAdmin(PrepareTimeout));

        var moduleConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForModule(login, "monolith-pings", 7, TimeSpan.FromMinutes(1)));

        adminConnectionStringBuilder.PersistSecurityInfo.ShouldBeFalse();
        moduleConnectionStringBuilder.PersistSecurityInfo.ShouldBeFalse();
    }

    [Fact]
    public void Admin_password_with_separators_survives()
    {
        var configuredConnectionStringBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = "db",
            Database = "monolith",
            Username = "postgres",
            Password = "a;b'c=d",
        };

        var connectionStrings = DatabaseConnectionStrings.From(
            CreateConfiguration(configuredConnectionStringBuilder.ConnectionString),
            Development);

        var adminConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
            connectionStrings.BuildForAdmin(PrepareTimeout));

        adminConnectionStringBuilder.Password.ShouldBe("a;b'c=d");
    }

    [Theory]
    [InlineData(";SSL Mode=VerifyFull")]
    [InlineData(";SSL Mode=VerifyCA")]
    [InlineData(";SSL Mode=Require")]
    [InlineData(";SSL Mode=Disable")]
    [InlineData(";sslmode=require")]
    [InlineData(";Ssl Mode=VerifyFull")]
    public void Explicit_ssl_mode_is_accepted_outside_development(String sslModeSetting)
    {
        var configuration = CreateConfiguration(ServerConnectionString + sslModeSetting);

        Should.NotThrow(() => DatabaseConnectionStrings.From(configuration, Production));
    }

    [Theory]
    [InlineData("", "Production")]
    [InlineData(";SSL Mode=Prefer", "Production")]
    [InlineData(";SSL Mode=Allow", "Production")]
    [InlineData(";sslmode=prefer", "Production")]
    [InlineData(";SSL Mode=Prefer", "Staging")]
    [InlineData("", "Qa")]
    public void Ssl_mode_that_may_fall_back_to_plain_text_is_refused_outside_development(
        String sslModeSetting,
        String environmentName)
    {
        var configuration = CreateConfiguration(ServerConnectionString + sslModeSetting);
        var hostEnvironment = new TestHostEnvironment(environmentName);

        var exception = Should.Throw<InvalidOperationException>(
            () => DatabaseConnectionStrings.From(configuration, hostEnvironment));

        exception.Message.ShouldContain("SSL Mode to VerifyFull, VerifyCA, Require or Disable outside Development");
    }

    [Fact]
    public void Development_accepts_a_connection_string_without_ssl_mode()
    {
        var configuration = CreateConfiguration(ServerConnectionString);

        Should.NotThrow(() => DatabaseConnectionStrings.From(configuration, Development));
    }

    private static IConfiguration CreateConfiguration(String? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<String, String?> { ["ConnectionStrings:database"] = connectionString })
            .Build();

    private sealed class TestHostEnvironment(String environmentName) : IHostEnvironment
    {
        public String EnvironmentName { get; set; } = environmentName;

        public String ApplicationName { get; set; } = "Tsikhanau.Monolith.UnitTests";

        public String ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
