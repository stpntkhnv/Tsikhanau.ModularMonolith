using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.Testing.Queues;
using Tsikhanau.Railway;
using Xunit;

namespace Tsikhanau.Monolith.Testing.App;

public abstract class AppFixture<TEntryPoint> : IAsyncLifetime
    where TEntryPoint : class
{
    public static readonly DateTimeOffset TestStartedOn = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly ModuleTestSetups _moduleTestSetups;
    private readonly Lazy<AppFactory<TEntryPoint>> _appFactory;
    private Optional<DatabaseResetter> _databaseResetter;

    protected AppFixture()
    {
        Time = new FakeTimeProvider(TestStartedOn);
        _moduleTestSetups = ModuleTestSetups.Create(GetType().Assembly);
        _appFactory = new Lazy<AppFactory<TEntryPoint>>(() => CreateFactoryWithSetups(Environments.Development));
    }

    public FakeTimeProvider Time { get; }

    public IServiceProvider Services => _appFactory.Value.Services;

    public String ConnectionString => _postgreSqlContainer.GetConnectionString();

    public DatabaseProbe Database => new(ConnectionString);

    public String ModuleGroupName => ModuleLogins.GetGroupName(Services);

    public HttpClient CreateClient() => _appFactory.Value.CreateClient();

    public Task ProcessQueuesAsync() => QueueDriver.ProcessAllAsync(Services);

    public Task ProcessQueuesAllowingFailuresAsync() => QueueDriver.ProcessAllAllowingFailuresAsync(Services);

    public Task ResetAsync()
    {
        ResetTime();
        return _databaseResetter.HasValue ? _databaseResetter.Value.ResetAsync() : Task.CompletedTask;
    }

    public ModuleDescriptor GetModule(String name) =>
        Services
            .GetRequiredService<ModuleRegistry>().Modules
            .Single(module => module.Name == name);

    public String GetModuleLoginName(String moduleName) =>
        ModuleLogins.GetLoginName(Services, GetModule(moduleName).Schema);

    public Task<UncommittedUpdate> ExecuteUncommittedAsync(String moduleName, String sql) =>
        UncommittedUpdate.ExecuteAsync(GetDataSource(moduleName), sql);

    public Task RedeliverInboxAsync(String moduleName, Guid messageId) =>
        InboxRedelivery.RedeliverAsync(GetDataSource(moduleName), GetModule(moduleName), messageId, Time.GetUtcNow());

    public AppFactory<TEntryPoint> StartFactory(String environmentName, IReadOnlyDictionary<String, String> overrides)
    {
        var appFactory = CreateFactoryWithSetups(environmentName);
        Start(appFactory, overrides);
        return appFactory;
    }

    public Exception CaptureStartupFailure(String environmentName, IReadOnlyDictionary<String, String> overrides)
    {
        using var appFactory = CreateFactoryWithSetups(environmentName);
        try
        {
            Start(appFactory, overrides);
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("The app started although its settings are invalid");
    }

    public async ValueTask InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync(TestContext.Current.CancellationToken);
        Start(_appFactory.Value, new Dictionary<String, String>());

        var schemas = Services
            .GetRequiredService<ModuleRegistry>().Modules
            .Select(module => module.Schema)
            .ToArray();

        _databaseResetter = schemas.Length == 0
            ? Optional<DatabaseResetter>.None()
            : await DatabaseResetter.CreateAsync(ConnectionString, schemas);
    }

    public async ValueTask DisposeAsync()
    {
        if (_appFactory.IsValueCreated)
        {
            await _appFactory.Value.DisposeAsync();
        }

        await _postgreSqlContainer.DisposeAsync();
    }

    protected abstract AppFactory<TEntryPoint> CreateFactory(TimeProvider timeProvider, String environmentName);

    private AppFactory<TEntryPoint> CreateFactoryWithSetups(String environmentName)
    {
        var appFactory = CreateFactory(Time, environmentName);
        appFactory.ModuleTestSetups = _moduleTestSetups;
        return appFactory;
    }

    private void ResetTime()
    {
        Time.AutoAdvanceAmount = TimeSpan.Zero;
        Time.SetLocalTimeZone(TimeZoneInfo.Utc);
        Time.AdjustTime(TestStartedOn);
    }

    private NpgsqlDataSource GetDataSource(String moduleName) =>
        Services.GetRequiredKeyedService<NpgsqlDataSource>(GetModule(moduleName).Key);

    private void Start(AppFactory<TEntryPoint> appFactory, IReadOnlyDictionary<String, String> overrides)
    {
        using var environmentOverride = new EnvironmentOverride(
            TestSettings.CreateEnvironmentVariables(ConnectionString, _moduleTestSetups.Settings, overrides));

        _ = appFactory.Services;
    }
}
