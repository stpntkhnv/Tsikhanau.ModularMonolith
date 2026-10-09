using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using ModularMonolith.Testing.Database;
using ModularMonolith.Testing.Queues;
using Npgsql;
using Testcontainers.PostgreSql;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Railway;
using Xunit;

namespace ModularMonolith.Testing.App;

public sealed class AppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly AppFactory _appFactory;
    private Optional<DatabaseResetter> _databaseResetter;

    public AppFixture()
    {
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero));
        _appFactory = new AppFactory(Time, Environments.Development);
    }

    public FakeTimeProvider Time { get; }

    public IServiceProvider Services => _appFactory.Services;

    public String ConnectionString => _postgreSqlContainer.GetConnectionString();

    public DatabaseProbe Database => new(ConnectionString);

    public HttpClient CreateClient() => _appFactory.CreateClient();

    public Task ProcessQueuesAsync() => QueueDriver.ProcessAllAsync(Services);

    public Task ProcessQueuesAllowingFailuresAsync() => QueueDriver.ProcessAllAllowingFailuresAsync(Services);

    public Task ResetAsync() =>
        _databaseResetter.HasValue ? _databaseResetter.Value.ResetAsync() : Task.CompletedTask;

    public ModuleDescriptor GetModule(String name) =>
        Services
            .GetRequiredService<ModuleRegistry>().Modules
            .Single(module => module.Name == name);

    public Task<UncommittedUpdate> ExecuteUncommittedAsync(String moduleName, String sql) =>
        UncommittedUpdate.ExecuteAsync(GetDataSource(moduleName), sql);

    public AppFactory StartFactory(String environmentName, IReadOnlyDictionary<String, String> overrides)
    {
        var appFactory = new AppFactory(Time, environmentName);
        Start(appFactory, overrides);
        return appFactory;
    }

    public Exception CaptureStartupFailure(String environmentName, IReadOnlyDictionary<String, String> overrides)
    {
        using var appFactory = new AppFactory(Time, environmentName);
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
        Start(_appFactory, new Dictionary<String, String>());

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
        await _appFactory.DisposeAsync();
        await _postgreSqlContainer.DisposeAsync();
    }

    private NpgsqlDataSource GetDataSource(String moduleName) =>
        Services.GetRequiredKeyedService<NpgsqlDataSource>(GetModule(moduleName).Key);

    private void Start(AppFactory appFactory, IReadOnlyDictionary<String, String> overrides)
    {
        var environmentVariables = new Dictionary<String, String>(
            TestSettings.CreateEnvironmentVariables(ConnectionString));

        foreach (var (name, value) in overrides)
        {
            environmentVariables[name] = value;
        }

        using var environmentOverride = new EnvironmentOverride(environmentVariables);
        _ = appFactory.Services;
    }
}
