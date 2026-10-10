using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.App;
using Tsikhanau.Monolith.TestModules.Pings.Settings;

namespace Tsikhanau.Monolith.IntegrationTests.App;

[Collection(TestAppCollection.Name)]
public sealed class StartupFailureTests(TestAppFixture testAppFixture) : TestAppTest(testAppFixture)
{
    private const Int32 CaptureCount = 50;

    private const String LimitError = "The field Limit must be between 1 and 100.";

    private const String PingsLimitFailure =
        $"DataAnnotation validation failed for '{nameof(PingsSettings)}' members: 'Limit' with the error: '{LimitError}'.";

    private static readonly TimeSpan DisposalTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void Factory_reports_the_failure_of_a_host_that_the_app_disposed_before_the_factory_started_it()
    {
        using var environmentOverride = new EnvironmentOverride(CreateEnvironmentVariables());
        using var appFactory = new StartAfterDisposalFactory(App.Time);

        var startupFailure = Should.Throw<OptionsValidationException>(() => appFactory.Services);

        startupFailure.Message.ShouldBe(PingsLimitFailure);
    }

    [Fact]
    public void Capture_returns_the_settings_failure_every_time()
    {
        var startupFailures = Enumerable
            .Range(0, CaptureCount)
            .Select(_ => CaptureInvalidPingsLimit())
            .Select(startupFailure => $"{startupFailure.GetType().Name}: {startupFailure.Message}")
            .ToArray();

        startupFailures.ShouldAllBe(startupFailure =>
            startupFailure == $"{nameof(OptionsValidationException)}: {PingsLimitFailure}");
    }

    private IReadOnlyDictionary<String, String> CreateEnvironmentVariables() =>
        new Dictionary<String, String>(TestSettings.CreateEnvironmentVariables(App.ConnectionString))
        {
            ["App__Modules__Pings__Limit"] = "0",
            ["App__Modules__Pongs__Limit"] = "10",
        };

    private Exception CaptureInvalidPingsLimit() =>
        App.CaptureStartupFailure(
            Environments.Development,
            new Dictionary<String, String> { ["App:Modules:Pings:Limit"] = "0" });

    private sealed class StartAfterDisposalFactory(TimeProvider timeProvider)
        : AppFactory<Program>(timeProvider, Environments.Development)
    {
        private readonly DisposalSignal _disposalSignal = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseContentRoot(AppContext.BaseDirectory);
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(_ => _disposalSignal));
        }

        protected override IHost CreateHost(IHostBuilder builder) =>
            base.CreateHost(new StartAfterDisposalHostBuilder(builder, _disposalSignal));
    }

    private sealed class DisposalSignal : ILoggerProvider
    {
        private readonly TaskCompletionSource _disposedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Disposed => _disposedSource.Task;

        public ILogger CreateLogger(String categoryName) => NullLogger.Instance;

        public void Dispose() => _disposedSource.TrySetResult();
    }

    private sealed class StartAfterDisposalHostBuilder(IHostBuilder hostBuilder, DisposalSignal disposalSignal)
        : IHostBuilder
    {
        public IDictionary<Object, Object> Properties => hostBuilder.Properties;

        public IHost Build()
        {
            var host = hostBuilder.Build();
            if (!disposalSignal.Disposed.Wait(DisposalTimeout))
            {
                throw new TimeoutException("The app did not dispose its host");
            }

            return host;
        }

        public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate) =>
            hostBuilder.ConfigureHostConfiguration(configureDelegate);

        public IHostBuilder ConfigureAppConfiguration(
            Action<HostBuilderContext, IConfigurationBuilder> configureDelegate) =>
            hostBuilder.ConfigureAppConfiguration(configureDelegate);

        public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate) =>
            hostBuilder.ConfigureServices(configureDelegate);

        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
            IServiceProviderFactory<TContainerBuilder> factory)
            where TContainerBuilder : notnull =>
            hostBuilder.UseServiceProviderFactory(factory);

        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
            Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory)
            where TContainerBuilder : notnull =>
            hostBuilder.UseServiceProviderFactory(factory);

        public IHostBuilder ConfigureContainer<TContainerBuilder>(
            Action<HostBuilderContext, TContainerBuilder> configureDelegate) =>
            hostBuilder.ConfigureContainer(configureDelegate);
    }
}
