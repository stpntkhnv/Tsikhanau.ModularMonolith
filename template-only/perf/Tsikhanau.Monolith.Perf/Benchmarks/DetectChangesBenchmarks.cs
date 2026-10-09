using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.Perf.Benchmarks;

[Config(typeof(InProcessConfig))]
public class DetectChangesBenchmarks
{
    private const String FakeConnectionString = "Host=127.0.0.1;Port=1;Database=x;Username=postgres;Password=postgres";

    private PingsDbContext _pingsDbContext = null!;
    private PlainDbContext _plainDbContext = null!;

    [Params(1000)]
    public Int32 Tracked { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var npgsqlDataSource = NpgsqlDataSource.Create(FakeConnectionString);
        var pingsOptionsBuilder = new DbContextOptionsBuilder<PingsDbContext>();
        ModuleDbContextOptions.Configure(pingsOptionsBuilder, npgsqlDataSource, PingsDbContext.SchemaName);
        _pingsDbContext = new PingsDbContext(pingsOptionsBuilder.Options);
        var plainOptionsBuilder = new DbContextOptionsBuilder<PlainDbContext>();
        ModuleDbContextOptions.Configure(plainOptionsBuilder, npgsqlDataSource, "plain");
        _plainDbContext = new PlainDbContext(plainOptionsBuilder.Options);
        Enumerable
            .Range(0, Tracked)
            .ForEach(AttachPings);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pingsDbContext.Dispose();
        _plainDbContext.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Boolean PlainPocoDetectChanges()
    {
        _plainDbContext.ChangeTracker.DetectChanges();
        return true;
    }

    [Benchmark]
    public Boolean DddPingDetectChanges()
    {
        _pingsDbContext.ChangeTracker.DetectChanges();
        return true;
    }

    private void AttachPings(Int32 index)
    {
        var label = $"l{index}";
        var ping = SamplePing.Create(label);
        ((IAggregateRoot)ping).ClearDomainEvents();
        _pingsDbContext.Attach(ping);
        _plainDbContext.Attach(new PlainPing { Id = Guid.CreateVersion7(), Label = label, Touches = 1, Version = 1 });
    }

    private sealed class PlainPing
    {
        public Guid Id { get; set; }

        public String Label { get; set; } = "";

        public Int32 Touches { get; set; }

        public Int32 Version { get; set; }

        public DateTimeOffset? ArchivedOn { get; set; }

        public String? Nickname { get; set; }
    }

    private sealed class PlainDbContext(DbContextOptions<PlainDbContext> dbContextOptions)
        : DbContext(dbContextOptions)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder
                .Entity<PlainPing>()
                .ToTable("plain_pings");
    }
}
