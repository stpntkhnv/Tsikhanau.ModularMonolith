using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Monolith.Infrastructure.Dispatching.Invokers;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Perf.Hosting;
using Tsikhanau.Monolith.Perf.Running;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Perf.Benchmarks;

[Config(typeof(InProcessConfig))]
public class DispatchBenchmarks
{
    private const String FakeConnectionString = "Host=127.0.0.1;Port=1;Database=x;Username=postgres;Password=postgres";

    private IHost _host = null!;
    private ILogger _logger = null!;
    private CreatePingCommandHandler _handler = null!;
    private CreatePingCommand _command = null!;
    private NpgsqlDataSource _npgsqlDataSource = null!;
    private ServiceProvider _plainServiceProvider = null!;
    private ServiceProvider _singletonOptionsServiceProvider = null!;
    private ServiceProvider _pooledServiceProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        var hostSettings = PerfOptions
            .Parse([])
            .GetHostSettings(runInBackground: false);

        _host = PerfHost.Build(FakeConnectionString, hostSettings);
        _logger = _host.Services.GetRequiredService<ILogger<ModuleDispatcher<IPingsModule>>>();
        _handler = new CreatePingCommandHandler(new NoopPingRepository(), TimeProvider.System);
        _command = new CreatePingCommand(Guid.CreateVersion7(), "label", Optional<String>.None());
        _npgsqlDataSource = NpgsqlDataSource.Create(FakeConnectionString);
        _plainServiceProvider = new ServiceCollection()
            .AddDbContext<PingsDbContext>(ConfigurePings)
            .BuildServiceProvider();

        _singletonOptionsServiceProvider = new ServiceCollection()
            .AddDbContext<PingsDbContext>(ConfigurePings, ServiceLifetime.Scoped, ServiceLifetime.Singleton)
            .BuildServiceProvider();

        _pooledServiceProvider = new ServiceCollection()
            .AddDbContextPool<PingsDbContext>(ConfigurePings)
            .BuildServiceProvider();

        using var scope = _host.Services.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<ModuleContext<IPingsModule>>().DbContext.Model;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _host.Dispose();
        _plainServiceProvider.Dispose();
        _singletonOptionsServiceProvider.Dispose();
        _pooledServiceProvider.Dispose();
        _npgsqlDataSource.Dispose();
    }

    [Benchmark]
    public async Task<Object> Scope()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return scope;
    }

    [Benchmark]
    public async Task<Object> ScopeAndDispatcher()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<ModuleDispatcher<IPingsModule>>();
    }

    [Benchmark]
    public async Task<Object> ScopeHandlerAndDbContextFirstUse()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreatePingCommand, Guid>>();
        _ = scope.ServiceProvider.GetRequiredService<ModuleContext<IPingsModule>>().DbContext.ChangeTracker;
        return handler;
    }

    [Benchmark]
    public Task<Object> DbContextAddDbContext() => AddPingAsync(_plainServiceProvider);

    [Benchmark]
    public Task<Object> DbContextAddDbContextSingletonOptions() => AddPingAsync(_singletonOptionsServiceProvider);

    [Benchmark]
    public Task<Object> DbContextAddDbContextPool() => AddPingAsync(_pooledServiceProvider);

    [Benchmark]
    public Object InvokerLookup() => CommandInvoker<Guid>.Get(typeof(CreatePingCommand));

    [Benchmark]
    public Double ActivityTimestampAndLog()
    {
        using var activity = AppDiagnostics.ActivitySource.StartActivity("Pings CreatePingCommand");
        var startedOn = Stopwatch.GetTimestamp();
        var elapsedMs = Stopwatch.GetElapsedTime(startedOn).TotalMilliseconds;
        DispatcherLog.Succeeded(_logger, "IPingsModule", "CreatePingCommand", elapsedMs);
        return elapsedMs;
    }

    [Benchmark]
    public Task<Result<Guid>> RailwayHandler() => _handler.Handle(_command, CancellationToken.None);

    private void ConfigurePings(DbContextOptionsBuilder dbContextOptionsBuilder) =>
        ModuleDbContextOptions.Configure(dbContextOptionsBuilder, _npgsqlDataSource, PingsDbContext.SchemaName);

    private static async Task<Object> AddPingAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PingsDbContext>();
        AddPing(dbContext);
        return dbContext;
    }

    private static void AddPing(PingsDbContext dbContext) =>
        dbContext
            .Set<Ping>()
            .Add(SamplePing.Create("label"));

    private sealed class NoopPingRepository : IPingRepository
    {
        public void Add(Ping ping) { }

        public Task<Result<Ping>> GetAsync(PingId pingId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
