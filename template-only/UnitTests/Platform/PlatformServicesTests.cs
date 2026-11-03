using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Infrastructure.Cleanup;
using Tsikhanau.Monolith.Infrastructure.Database.Settings;
using Tsikhanau.Monolith.Infrastructure.Platform;
using Tsikhanau.Monolith.Infrastructure.Queues;

namespace Tsikhanau.Monolith.UnitTests.Platform;

public sealed class PlatformServicesTests
{
    private const String ConnectionStringKey = "ConnectionStrings:database";

    [Fact]
    public void Repeated_platform_services_validate_each_platform_settings_record_once()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection()
            .AddPlatformServices(configuration)
            .AddPlatformServices(configuration);

        var validatorCounts = DescribeValidatorCounts(services);

        validatorCounts.ShouldBe(["QueueSettings 1", "CleanupSettings 1", "DatabaseSettings 1"]);
    }

    [Fact]
    public void Repeated_platform_validates_each_platform_settings_record_once()
    {
        var builder = CreateDevelopmentBuilder();
        builder.Configuration[ConnectionStringKey] = "Host=db;Database=checked;Username=postgres;Password=p";
        builder
            .AddPlatform()
            .AddPlatform();

        var validatorCounts = DescribeValidatorCounts(builder.Services);

        validatorCounts.ShouldBe(["QueueSettings 1", "CleanupSettings 1", "DatabaseSettings 1"]);
    }

    [Fact]
    public void Connection_strings_come_from_the_configuration_the_host_was_built_with()
    {
        var builder = CreateDevelopmentBuilder();
        builder.Configuration[ConnectionStringKey] = "Host=db;Database=checked;Username=postgres;Password=p";
        builder.EnsureConnectionString();
        builder.Services.AddPlatformServices(builder.Configuration);
        builder.Configuration[ConnectionStringKey] = "Host=db;Database=final;Username=postgres;Password=p";
        using var host = builder.Build();

        var databaseName = host.Services.GetRequiredService<DatabaseConnectionStrings>().DatabaseName;

        databaseName.ShouldBe("final");
    }

    private static HostApplicationBuilder CreateDevelopmentBuilder() =>
        new(new HostApplicationBuilderSettings { DisableDefaults = true, EnvironmentName = Environments.Development });

    private static IReadOnlyList<String> DescribeValidatorCounts(IServiceCollection services) =>
    [
        DescribeValidatorCount<QueueSettings>(services),
        DescribeValidatorCount<CleanupSettings>(services),
        DescribeValidatorCount<DatabaseSettings>(services),
    ];

    private static String DescribeValidatorCount<TSettings>(IServiceCollection services)
        where TSettings : class =>
        $"{typeof(TSettings).Name} {services.Count(IsValidatorOf<TSettings>)}";

    private static Boolean IsValidatorOf<TSettings>(ServiceDescriptor descriptor)
        where TSettings : class =>
        descriptor.ServiceType == typeof(IValidateOptions<TSettings>);
}
