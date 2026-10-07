using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.BuildingBlocks.IntegrationTests.Hosting;
using ModularMonolith.Testing.Database;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using ModularMonolith.TestModules.Pings.Features.ReschedulePing;
using ModularMonolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.IntegrationTests.Persistence;

public sealed class PersistenceTests(PostgresFixture postgresFixture)
{
    private const String QueueTableStorageParametersSql = """
        SELECT n.nspname || '.' || c.relname || ' ' || coalesce(array_to_string(c.reloptions, ','), '')
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname IN ('pings', 'pongs')
          AND c.relname IN ('outbox_messages', 'inbox_messages', 'internal_commands')
        """;

    private const String VacuumAfterFixedDeadRows =
        "autovacuum_vacuum_scale_factor=0,autovacuum_vacuum_threshold=10000";

    private static readonly DateTimeOffset TestStartedOn = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Module_schema_holds_its_tables_and_the_three_queues()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);

        var tableNames = await new DatabaseProbe(postgresFixture.ConnectionString).ReadTableNamesAsync("pings");

        tableNames.ShouldBe(
            ["__ef_migrations_history", "echoes", "inbox_messages", "internal_commands", "outbox_messages", "pings"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Queue_tables_are_vacuumed_after_a_fixed_number_of_dead_rows()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);

        var storageParameters = await testModulesHost.Database.QueryAsync<String>(QueueTableStorageParametersSql);

        storageParameters.ShouldBe(
            [
                $"pings.outbox_messages {VacuumAfterFixedDeadRows}",
                $"pings.inbox_messages {VacuumAfterFixedDeadRows}",
                $"pings.internal_commands {VacuumAfterFixedDeadRows}",
                $"pongs.outbox_messages {VacuumAfterFixedDeadRows}",
                $"pongs.inbox_messages {VacuumAfterFixedDeadRows}",
                $"pongs.internal_commands {VacuumAfterFixedDeadRows}",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Typed_ids_and_value_objects_round_trip()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await SavePingAsync(testModulesHost, "hello");
        await using var scope = testModulesHost.CreateScope();

        var loadedPing = await LoadAsync(ResolvePingsDbContext(scope), pingId);

        loadedPing.Label.ShouldBe(CreateLabel("hello"));
        loadedPing.Touches.ShouldBe(0);
        loadedPing.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Version_guards_against_lost_updates()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await SavePingAsync(testModulesHost, "race");
        await using var firstScope = testModulesHost.CreateScope();
        await using var secondScope = testModulesHost.CreateScope();
        var firstDbContext = ResolvePingsDbContext(firstScope);
        var secondDbContext = ResolvePingsDbContext(secondScope);
        var firstPing = await LoadAsync(firstDbContext, pingId);
        var secondPing = await LoadAsync(secondDbContext, pingId);

        await TouchAndSaveAsync(firstDbContext, firstPing);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => TouchAndSaveAsync(secondDbContext, secondPing));
    }

    [Fact]
    public async Task Optional_state_round_trips_through_nullable_columns()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var plainPingId = await SavePingAsync(testModulesHost, "plain");
        var fullPingId = await SavePingAsync(testModulesHost, "full");
        await ArchiveAndGiveNicknameAsync(testModulesHost, fullPingId, "nick");
        await using var scope = testModulesHost.CreateScope();

        var loadedPlainPing = await LoadAsync(ResolvePingsDbContext(scope), plainPingId);
        var loadedFullPing = await LoadAsync(ResolvePingsDbContext(scope), fullPingId);

        loadedPlainPing.ArchivedOn.IsNone.ShouldBeTrue();
        loadedPlainPing.Nickname.IsNone.ShouldBeTrue();
        loadedFullPing.ArchivedOn.ShouldBe(Optional<DateTimeOffset>.Some(TestStartedOn));
        loadedFullPing.Nickname.ShouldBe(Optional<PingLabel>.Some(CreateLabel("nick")));
    }

    [Fact]
    public async Task Optional_state_is_stored_in_nullable_snake_case_columns()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);

        var columns = await testModulesHost.Database.QueryAsync<String>("""
            SELECT column_name || ':' || is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'pings' AND table_name = 'pings' AND column_name IN ('archived_on', 'nickname')
            """);

        columns.ShouldBe(["archived_on:YES", "nickname:YES"], ignoreOrder: true);
    }

    [Fact]
    public async Task Composite_value_object_round_trips_through_columns_of_its_owner()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var pingId = await testModulesHost.CreatePingAsync("scheduled");
        await RescheduleAsync(testModulesHost, pingId, TimeSpan.FromSeconds(30), 5);
        await using var scope = testModulesHost.CreateScope();

        var loadedPing = await LoadAsync(ResolvePingsDbContext(scope), PingId.From(pingId));

        loadedPing.Schedule.ShouldBe(CreateSchedule(TimeSpan.FromSeconds(30), 5));
    }

    private static PingsDbContext ResolvePingsDbContext(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<PingsDbContext>();

    private static async Task<PingId> SavePingAsync(TestModulesHost testModulesHost, String label)
    {
        var pingId = PingId.New(TestStartedOn);
        await using var scope = testModulesHost.CreateScope();
        var dbContext = ResolvePingsDbContext(scope);
        dbContext
            .Set<Ping>()
            .Add(Ping.Create(pingId, CreateLabel(label), TestStartedOn));

        await dbContext.SaveChangesAsync(CancellationToken);
        return pingId;
    }

    private static Task<Ping> LoadAsync(PingsDbContext dbContext, PingId pingId) =>
        dbContext
            .Set<Ping>()
            .SingleAsync(ping => ping.Id == pingId, CancellationToken);

    private static async Task TouchAndSaveAsync(PingsDbContext dbContext, Ping ping)
    {
        var touchResult = ping.Touch(TestStartedOn);
        dbContext
            .Entry(ping)
            .BumpVersion();

        touchResult.IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken);
    }

    private static async Task ArchiveAndGiveNicknameAsync(
        TestModulesHost testModulesHost,
        PingId pingId,
        String nickname)
    {
        await using var scope = testModulesHost.CreateScope();
        var dbContext = ResolvePingsDbContext(scope);
        var ping = await LoadAsync(dbContext, pingId);

        var archiveResult = ping.Archive(TestStartedOn);
        var nicknameResult = ping.GiveNickname(CreateLabel(nickname));

        archiveResult.IsSuccess.ShouldBeTrue();
        nicknameResult.IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync(CancellationToken);
    }

    private static async Task RescheduleAsync(
        TestModulesHost testModulesHost,
        Guid pingId,
        TimeSpan interval,
        Int32 repeats)
    {
        var result = await testModulesHost.Pings.ExecuteCommandAsync(
            new ReschedulePingCommand(pingId, interval, repeats),
            CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    private static PingLabel CreateLabel(String value) => PingLabel.Create(value, "label").Value;

    private static PingSchedule CreateSchedule(TimeSpan interval, Int32 repeats) =>
        PingSchedule.Create(interval, repeats, "schedule").Value;
}
