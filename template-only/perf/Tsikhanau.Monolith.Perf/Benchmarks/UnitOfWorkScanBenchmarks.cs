using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Domain.Events;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Perf.Scenarios;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.Perf.Benchmarks;

[Config(typeof(InProcessConfig))]
public class UnitOfWorkScanBenchmarks
{
    private const String FakeConnectionString = "Host=127.0.0.1;Port=1;Database=x;Username=postgres;Password=postgres";

    private readonly HashSet<DomainEvent> _dispatchedEvents = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<IAggregateRoot> _rootsWithEvents = new(ReferenceEqualityComparer.Instance);
    private PingsDbContext _dbContext = null!;

    [Params(1, 100, 1000)]
    public Int32 TrackedAggregates { get; set; }

    [Params(0, 200)]
    public Int32 AddedOwnedParts { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<PingsDbContext>();
        ModuleDbContextOptions.Configure(
            dbContextOptionsBuilder,
            NpgsqlDataSource.Create(FakeConnectionString),
            PingsDbContext.SchemaName);

        _dbContext = new PingsDbContext(dbContextOptionsBuilder.Options);
        Enumerable
            .Range(0, TrackedAggregates - 1)
            .ForEach(AttachExistingPing);

        var createdPing = SamplePing.Create("new");
        _dbContext.Add(createdPing);
        _dbContext.Add(CreateOutboxMessage());
        AddEchoesToFirstPing();
        _rootsWithEvents.Add(createdPing);
    }

    [GlobalCleanup]
    public void Cleanup() => _dbContext.Dispose();

    [Benchmark(Baseline = true)]
    public Int32 CurrentScans()
    {
        var firstRound = GetPendingEvents();
        MarkDispatched(firstRound);
        var secondRound = GetPendingEvents();
        var changedEntries = _dbContext.ChangeTracker.GetChangedAggregateEntries(_rootsWithEvents);
        var hasNewQueueMessages = HasNewQueueMessages();
        _dispatchedEvents.Clear();
        return firstRound.Count + secondRound.Count + changedEntries.Count + (hasNewQueueMessages ? 1 : 0);
    }

    [Benchmark]
    public Int32 SingleDetectChanges()
    {
        var changeTracker = _dbContext.ChangeTracker;
        changeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var firstRound = GetPendingEvents();
            MarkDispatched(firstRound);
            var secondRound = GetPendingEvents();
            changeTracker.DetectChanges();
            var changedEntries = changeTracker.GetChangedAggregateEntries(_rootsWithEvents);
            var hasNewQueueMessages = HasNewQueueMessages();
            _dispatchedEvents.Clear();
            return firstRound.Count + secondRound.Count + changedEntries.Count + (hasNewQueueMessages ? 1 : 0);
        }
        finally
        {
            changeTracker.AutoDetectChangesEnabled = true;
        }
    }

    [Benchmark]
    public Int32 OneDetectChangesOnly()
    {
        _dbContext.ChangeTracker.DetectChanges();
        return 0;
    }

    private void AttachExistingPing(Int32 index)
    {
        var existingPing = SamplePing.Create($"l{index}");
        ((IAggregateRoot)existingPing).ClearDomainEvents();
        _dbContext.Attach(existingPing);
        var entry = _dbContext.Entry(existingPing);
        entry.Property<Int32>("Version").CurrentValue = 1;
        entry.State = EntityState.Unchanged;
    }

    private void AddEchoesToFirstPing()
    {
        if (AddedOwnedParts == 0)
        {
            return;
        }

        var ping = _dbContext.ChangeTracker
            .Entries<Ping>()
            .First(entry => entry.State == EntityState.Unchanged || TrackedAggregates == 1)
            .Entity;

        Enumerable
            .Range(0, AddedOwnedParts)
            .ForEach(echo => AddEcho(ping, echo));

        _dbContext.ChangeTracker.DetectChanges();
    }

    private void MarkDispatched(List<(IAggregateRoot Root, DomainEvent Event)> pendingEvents)
    {
        foreach (var (_, domainEvent) in pendingEvents)
        {
            _dispatchedEvents.Add(domainEvent);
        }
    }

    private List<(IAggregateRoot Root, DomainEvent Event)> GetPendingEvents() =>
        _dbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .SelectMany(entry => entry.Entity.DomainEvents.Select(domainEvent => (entry.Entity, domainEvent)))
            .Where(pendingEvent => !_dispatchedEvents.Contains(pendingEvent.domainEvent))
            .ToList();

    private Boolean HasNewQueueMessages() =>
        _dbContext.ChangeTracker
            .Entries()
            .Any(entry => entry.State == EntityState.Added && entry.Entity is OutboxMessage or InternalCommandMessage);

    private static void AddEcho(Ping ping, Int32 echo) =>
        ping
            .AddEcho(EchoId.From(Guid.CreateVersion7()), $"echo {echo}")
            .GetValueOrThrow();

    private static OutboxMessage CreateOutboxMessage() =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Type = "pings.ping-created",
            Payload = "{}",
            NextAttemptOn = DateTimeOffset.UtcNow,
            OccurredOn = DateTimeOffset.UtcNow,
        };
}
