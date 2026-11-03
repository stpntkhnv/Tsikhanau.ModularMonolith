using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Persistence.Commits;
using Tsikhanau.Monolith.IntegrationTests.Hosts.InProcess;
using Tsikhanau.Monolith.IntegrationTests.Postgres;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

public sealed class TwoVersionsTests(PostgresFixture postgresFixture)
{
    private const String Label = "later release";
    private const String PingCreatedType = "pings.ping-created";
    private const String UnknownTypeCode = "Messaging.UnknownType";

    private const String ReadOutboxLastErrorSql = "SELECT last_error FROM pings.outbox_messages WHERE type = @Type";

    private const String ReadInboxLastErrorSql = "SELECT last_error FROM pongs.inbox_messages WHERE type = @Type";

    private const String ReadPongLabelsSql = "SELECT label FROM pongs.pongs WHERE ping_id = @PingId";

    private static readonly TimeSpan PastTheFirstBackoff = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan PastEveryLeaseAndBackoff = TimeSpan.FromHours(1);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task New_event_claimed_first_by_an_older_instance_reaches_its_new_subscriber_exactly_once()
    {
        await using var olderInstance = await TestModulesHost.StartAsync(postgresFixture);
        await using var newerInstance = await TestModulesHost.StartAsync(
            postgresFixture,
            configure: PingOfALaterReleaseSubscription.AddToPongs);

        var publishedEvent = await PublishPingOfALaterReleaseAsync(newerInstance);

        await olderInstance.DeliverOutboxAsync();
        var claimedOutboxRows = await olderInstance.ReadOutboxRowsAsync();
        var claimedLastError = await ReadOutboxLastErrorAsync(
            olderInstance,
            PingOfALaterReleaseIntegrationEvent.Name);

        var inboxRowsAfterClaim = await olderInstance.ReadInboxRowsAsync();
        Advance(PastTheFirstBackoff, olderInstance, newerInstance);

        await newerInstance.DeliverOutboxAsync();
        var deliveredOutboxRows = await newerInstance.ReadOutboxRowsAsync();
        await olderInstance.ProcessInboxAsync(CancellationToken);
        var claimedInboxRows = await olderInstance.ReadInboxRowsAsync();
        var claimedInboxLastError = await ReadInboxLastErrorAsync(
            olderInstance,
            PingOfALaterReleaseIntegrationEvent.Name);

        var pongLabelsAfterInboxClaim = await ReadPongLabelsAsync(olderInstance, publishedEvent.PingId);
        Advance(PastTheFirstBackoff, olderInstance, newerInstance);

        await newerInstance.ProcessQueuesAsync();
        var handledInboxRows = await newerInstance.ReadInboxRowsAsync();
        var pongLabels = await ReadPongLabelsAsync(newerInstance, publishedEvent.PingId);
        Advance(PastEveryLeaseAndBackoff, olderInstance, newerInstance);

        await olderInstance.ProcessQueuesAsync();
        await newerInstance.ProcessQueuesAsync();
        var outboxRowsAfterAnotherPass = await olderInstance.ReadOutboxRowsAsync();
        var inboxRowsAfterAnotherPass = await olderInstance.ReadInboxRowsAsync();
        var pongLabelsAfterAnotherPass = await ReadPongLabelsAsync(olderInstance, publishedEvent.PingId);

        var claimedRow = claimedOutboxRows.ShouldHaveSingleItem();
        claimedRow.Id.ShouldBe(publishedEvent.Id);
        claimedRow.Type.ShouldBe(PingOfALaterReleaseIntegrationEvent.Name);
        claimedRow.Attempts.ShouldBe(1);
        claimedRow.IsProcessed.ShouldBeFalse();
        claimedRow.HasFailed.ShouldBeFalse();
        claimedLastError.ShouldStartWith($"{UnknownTypeCode}: ");
        inboxRowsAfterClaim.ShouldBeEmpty();
        var deliveredRow = deliveredOutboxRows.ShouldHaveSingleItem();
        deliveredRow.Attempts.ShouldBe(2);
        deliveredRow.IsProcessed.ShouldBeTrue();
        var claimedInboxRow = claimedInboxRows.ShouldHaveSingleItem();
        claimedInboxRow.Id.ShouldBe(publishedEvent.Id);
        claimedInboxRow.Attempts.ShouldBe(1);
        claimedInboxRow.IsProcessed.ShouldBeFalse();
        claimedInboxRow.HasFailed.ShouldBeFalse();
        claimedInboxLastError.ShouldStartWith($"{UnknownTypeCode}: ");
        pongLabelsAfterInboxClaim.ShouldBeEmpty();
        var handledRow = handledInboxRows.ShouldHaveSingleItem();
        handledRow.Id.ShouldBe(publishedEvent.Id);
        handledRow.Attempts.ShouldBe(2);
        handledRow.IsProcessed.ShouldBeTrue();
        pongLabels.ShouldBe([Label]);
        outboxRowsAfterAnotherPass.ShouldBe(deliveredOutboxRows);
        inboxRowsAfterAnotherPass.ShouldBe(handledInboxRows);
        pongLabelsAfterAnotherPass.ShouldBe([Label]);
    }

    [Fact]
    public async Task Documented_limit_an_older_instance_marks_an_existing_event_processed_without_its_new_subscriber()
    {
        await using var newerInstance = await TestModulesHost.StartAsync(postgresFixture);
        await using var olderInstance = await TestModulesHost.StartPingsOnlyAsync(postgresFixture);
        await newerInstance.CreatePingAsync("existing event");

        await olderInstance.DeliverOutboxAsync();
        var outboxRows = await olderInstance.ReadOutboxRowsAsync();
        var lastError = await ReadOutboxLastErrorAsync(olderInstance, PingCreatedType);
        var inboxRowsAfterDelivery = await newerInstance.ReadInboxRowsAsync();
        Advance(PastTheFirstBackoff, olderInstance, newerInstance);

        await newerInstance.ProcessQueuesAsync();
        var inboxRows = await newerInstance.ReadInboxRowsAsync();
        var pongCount = await newerInstance.CountPongsAsync();

        var outboxRow = outboxRows.ShouldHaveSingleItem();
        outboxRow.Type.ShouldBe(PingCreatedType);
        outboxRow.Attempts.ShouldBe(1);
        outboxRow.IsProcessed.ShouldBeTrue();
        lastError.ShouldBeNull();
        inboxRowsAfterDelivery.ShouldBeEmpty();
        inboxRows.ShouldBeEmpty();
        pongCount.ShouldBe(0);
        newerInstance.PongHooks.ReceivedPingIds.ShouldBeEmpty();
    }

    private static async Task<PingOfALaterReleaseIntegrationEvent> PublishPingOfALaterReleaseAsync(
        TestModulesHost testModulesHost)
    {
        var publishedOn = testModulesHost.Time.GetUtcNow();
        var publishedEvent = new PingOfALaterReleaseIntegrationEvent(
            publishedOn,
            Guid.CreateVersion7(publishedOn),
            Label);

        await using var scope = testModulesHost.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<IOutbox<IPingsModule>>()
            .Add(publishedEvent);

        var committed = await scope.ServiceProvider
            .GetRequiredService<IUnitOfWork<IPingsModule>>()
            .CommitAsync(CancellationToken);

        committed.IsSuccess.ShouldBeTrue();
        return publishedEvent;
    }

    private static void Advance(TimeSpan elapsed, TestModulesHost olderInstance, TestModulesHost newerInstance)
    {
        olderInstance.Time.Advance(elapsed);
        newerInstance.Time.Advance(elapsed);
    }

    private static Task<String?> ReadOutboxLastErrorAsync(TestModulesHost testModulesHost, String type) =>
        testModulesHost.Database.ExecuteScalarAsync<String?>(ReadOutboxLastErrorSql, new { Type = type });

    private static Task<String?> ReadInboxLastErrorAsync(TestModulesHost testModulesHost, String type) =>
        testModulesHost.Database.ExecuteScalarAsync<String?>(ReadInboxLastErrorSql, new { Type = type });

    private static Task<IReadOnlyList<String>> ReadPongLabelsAsync(TestModulesHost testModulesHost, Guid pingId) =>
        testModulesHost.Database.QueryAsync<String>(ReadPongLabelsSql, new { PingId = pingId });

    [IntegrationEventName(Name)]
    private sealed record PingOfALaterReleaseIntegrationEvent(DateTimeOffset OccurredOn, Guid PingId, String Label)
        : IntegrationEvent(OccurredOn)
    {
        public const String Name = "tests.ping-of-a-later-release";
    }

    private sealed class PingOfALaterReleaseIntegrationEventHandler(
        IPongRepository pongRepository,
        TimeProvider timeProvider)
        : IIntegrationEventHandler<PingOfALaterReleaseIntegrationEvent>
    {
        public Task<Result<Unit>> Handle(
            PingOfALaterReleaseIntegrationEvent integrationEvent,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                PongLabel
                    .Create(integrationEvent.Label, "label")
                    .Map(label => CreatePong(integrationEvent.PingId, label))
                    .Tap(pongRepository.Add)
                    .Map(_ => Unit.Value));

        private Pong CreatePong(Guid pingId, PongLabel label)
        {
            var createdOn = timeProvider.GetUtcNow();
            return Pong.Create(PongId.From(Guid.CreateVersion7(createdOn)), pingId, label, createdOn);
        }
    }

    private static class PingOfALaterReleaseSubscription
    {
        public static void AddToPongs(IHostApplicationBuilder builder)
        {
            builder.Services.AddKeyedScoped<
                IIntegrationEventHandler<PingOfALaterReleaseIntegrationEvent>,
                PingOfALaterReleaseIntegrationEventHandler>(typeof(IPongsModule));

            builder.Services
                .GetOrAddInstance<IntegrationEventRegistry>()
                .Register(typeof(PingOfALaterReleaseIntegrationEvent));

            builder.Services
                .GetOrAddInstance<SubscriptionRegistry>()
                .Subscribe(
                    PingOfALaterReleaseIntegrationEvent.Name,
                    GetPongs(builder.Services),
                    runsOutsideTransaction: false);
        }

        private static ModuleDescriptor GetPongs(IServiceCollection services) =>
            services
                .GetOrAddInstance<ModuleRegistry>().Modules
                .Single(module => module.Key == typeof(IPongsModule));
    }
}
