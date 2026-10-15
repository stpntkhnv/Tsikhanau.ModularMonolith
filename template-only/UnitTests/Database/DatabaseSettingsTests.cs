using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Platform;
using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.UnitTests.Database;

public sealed class DatabaseSettingsTests
{
    private const String LoginKeyOf31Bytes = "dGhpcnR5LW9uZS1ieXRlcy1pcy1vbmUtdG9vLWZldw==";

    private const String LoginKeyAdvice =
        "base64 of at least 32 random bytes (openssl rand -base64 32), kept as a secret: "
        + "the AppHost passes its login-key parameter, a server sets App__Database__LoginKey";

    [Fact]
    public void Prepare_timeout_comes_from_configuration()
    {
        var settings = ResolveSettings(
            new()
            {
                ["App:Database:LoginKey"] = TestSettings.LoginKey,
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
                    ["App:Database:LoginKey"] = TestSettings.LoginKey,
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
                    ["App:Database:LoginKey"] = TestSettings.LoginKey,
                    ["App:Database:MaxConnections"] = "20",
                    ["App:Database:PrepareTimeout"] = prepareTimeout,
                }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_login_key_is_refused_with_what_to_set(String? loginKey)
    {
        var exception = Should.Throw<OptionsValidationException>(() => ResolveSettingsWithLoginKey(loginKey));

        exception.Message.ShouldContain($"App:Database:LoginKey is required: {LoginKeyAdvice}");
    }

    [Theory]
    [InlineData("not base64 at all")]
    [InlineData(LoginKeyOf31Bytes)]
    public void Login_key_that_is_not_base64_of_32_bytes_is_refused(String loginKey)
    {
        var exception = Should.Throw<OptionsValidationException>(() => ResolveSettingsWithLoginKey(loginKey));

        exception.Message.ShouldContain($"App:Database:LoginKey must be {LoginKeyAdvice}");
    }

    [Fact]
    public void Login_key_of_32_bytes_is_accepted() =>
        Should.NotThrow(() => ResolveSettingsWithLoginKey(TestSettings.LoginKey));

    [Fact]
    public void Text_form_never_shows_the_login_key()
    {
        var settings = ResolveSettingsWithLoginKey(TestSettings.LoginKey);

        var text = settings.ToString();

        text.ShouldNotContain(TestSettings.LoginKey);
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
            LoginKey = TestSettings.LoginKey,
            MaxConnections = maxConnections,
            PrepareTimeout = TimeSpan.FromMinutes(10),
        };

        var calculatedPoolSizePerModule = settings.CalculatePoolSizePerModule(moduleCount);

        calculatedPoolSizePerModule.ShouldBe(poolSizePerModule);
    }

    private static DatabaseSettings ResolveSettingsWithLoginKey(String? loginKey) =>
        ResolveSettings(
            new()
            {
                ["App:Database:LoginKey"] = loginKey,
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
