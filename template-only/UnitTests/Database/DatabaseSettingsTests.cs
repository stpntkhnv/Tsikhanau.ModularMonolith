using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Platform;

namespace Tsikhanau.Monolith.UnitTests.Database;

public sealed class DatabaseSettingsTests
{
    private const String IdentifierRuleMessage =
        "App:Database:LoginPrefix must start with a letter, hold only a-z, 0-9 and _, "
        + "and be neither pg nor start with pg_, which PostgreSQL reserves";

    [Fact]
    public void Prepare_timeout_comes_from_configuration()
    {
        var settings = ResolveSettings(
            new()
            {
                ["App:Database:LoginPrefix"] = "monolith",
                ["App:Database:MaxConnections"] = "20",
                ["App:Database:PrepareTimeout"] = "00:05:00",
            });

        settings.PrepareTimeout.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Missing_prepare_timeout_is_rejected() =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(
                new()
                {
                    ["App:Database:LoginPrefix"] = "monolith",
                    ["App:Database:MaxConnections"] = "20",
                }));

    [Theory]
    [InlineData("00:00:09")]
    [InlineData("01:00:01")]
    public void Prepare_timeout_outside_ten_seconds_to_an_hour_is_rejected(String prepareTimeout) =>
        Should.Throw<OptionsValidationException>(
            () => ResolveSettings(
                new()
                {
                    ["App:Database:LoginPrefix"] = "monolith",
                    ["App:Database:MaxConnections"] = "20",
                    ["App:Database:PrepareTimeout"] = prepareTimeout,
                }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_login_prefix_is_refused(String? loginPrefix)
    {
        var exception = Should.Throw<OptionsValidationException>(() => ResolveSettingsWithLoginPrefix(loginPrefix));

        exception.Message.ShouldContain("App:Database:LoginPrefix is required");
    }

    [Theory]
    [InlineData("1monolith")]
    [InlineData("Monolith")]
    [InlineData("mono-lith")]
    public void Login_prefix_that_is_not_a_plain_identifier_is_refused(String loginPrefix)
    {
        var exception = Should.Throw<OptionsValidationException>(() => ResolveSettingsWithLoginPrefix(loginPrefix));

        exception.Message.ShouldContain(IdentifierRuleMessage);
    }

    [Theory]
    [InlineData("pg")]
    [InlineData("pg_tools")]
    public void Login_prefix_whose_roles_postgres_reserves_is_refused(String loginPrefix)
    {
        var exception = Should.Throw<OptionsValidationException>(() => ResolveSettingsWithLoginPrefix(loginPrefix));

        exception.Message.ShouldContain(IdentifierRuleMessage);
    }

    [Theory]
    [InlineData("pgx")]
    [InlineData("p_g")]
    public void Login_prefix_that_only_starts_like_a_reserved_name_is_accepted(String loginPrefix)
    {
        var settings = ResolveSettingsWithLoginPrefix(loginPrefix);

        settings.LoginPrefix.ShouldBe(loginPrefix);
    }

    [Fact]
    public void Login_prefix_longer_than_30_characters_is_refused()
    {
        var exception = Should.Throw<OptionsValidationException>(
            () => ResolveSettingsWithLoginPrefix(new String('a', 31)));

        exception.Message.ShouldContain("App:Database:LoginPrefix must be at most 30 characters long");
    }

    [Fact]
    public void Login_prefix_of_30_characters_is_accepted()
    {
        var loginPrefix = new String('a', 30);

        var settings = ResolveSettingsWithLoginPrefix(loginPrefix);

        settings.LoginPrefix.ShouldBe(loginPrefix);
    }

    [Theory]
    [InlineData(20, 2, 10)]
    [InlineData(21, 2, 10)]
    [InlineData(3, 2, 2)]
    public void Pool_size_splits_the_budget_evenly_and_never_drops_below_two_per_module(
        Int32 maxConnections,
        Int32 moduleCount,
        Int32 poolSizePerModule)
    {
        var settings = new DatabaseSettings
        {
            LoginPrefix = "monolith",
            MaxConnections = maxConnections,
            PrepareTimeout = TimeSpan.FromMinutes(10),
        };

        var calculatedPoolSizePerModule = settings.CalculatePoolSizePerModule(moduleCount);

        calculatedPoolSizePerModule.ShouldBe(poolSizePerModule);
    }

    private static DatabaseSettings ResolveSettingsWithLoginPrefix(String? loginPrefix) =>
        ResolveSettings(
            new()
            {
                ["App:Database:LoginPrefix"] = loginPrefix,
                ["App:Database:MaxConnections"] = "20",
                ["App:Database:PrepareTimeout"] = "00:10:00",
            });

    private static DatabaseSettings ResolveSettings(Dictionary<String, String?> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        return new ServiceCollection()
            .AddPlatformServices(configuration)
            .BuildServiceProvider()
            .GetRequiredService<IOptions<DatabaseSettings>>().Value;
    }
}
