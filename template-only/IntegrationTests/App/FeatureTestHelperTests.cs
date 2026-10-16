using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings.Events;
using Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;
using Tsikhanau.Monolith.TestModules.Pings.Features.ScheduleTouch;
using Tsikhanau.Monolith.TestModules.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Features;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.App;

[Collection(TestAppCollection.Name)]
public sealed class FeatureTestHelperTests(TestAppFixture testAppFixture) : TestAppTest(testAppFixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private IPingsModule PingsModule => App.Services.GetRequiredService<IPingsModule>();

    private PongTestHooks PongTestHooks => App.Services.GetRequiredService<PongTestHooks>();

    private String PingsSchema => App.GetModule("Pings").Schema;

    private String PongsSchema => App.GetModule("Pongs").Schema;

    [Fact]
    public async Task Published_integration_event_reaches_the_subscriber_through_its_inbox()
    {
        var pingId = Guid.CreateVersion7();
        var integrationEvent = new PingCreatedIntegrationEvent(
            TestAppFixture.TestStartedOn,
            pingId,
            "published",
            Optional<String>.None());

        await App.PublishAsync(integrationEvent);
        var deliveredMessage = await ReadSingleQueueRowAsync(PongsSchema, InboxMessage.Table);
        var handledBeforeQueues = CountHandled(pingId);
        await App.ProcessQueuesAsync();
        var processedMessage = await ReadSingleQueueRowAsync(PongsSchema, InboxMessage.Table);
        var handledAfterQueues = CountHandled(pingId);
        var pingsOutboxMessages = await App.Database.ReadQueueRowsAsync(PingsSchema, OutboxMessage.Table);

        deliveredMessage.ShouldBe(
            new QueueRow(
                integrationEvent.Id,
                "pings.ping-created",
                Attempts: 0,
                IsProcessed: false,
                HasFailed: false,
                NextAttemptOn: TestAppFixture.TestStartedOn.UtcDateTime,
                Payload: BuildPingCreatedPayload(integrationEvent.Id, pingId, "published")));

        handledBeforeQueues.ShouldBe(0);
        processedMessage.IsProcessed.ShouldBeTrue();
        handledAfterQueues.ShouldBe(1);
        pingsOutboxMessages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Published_secret_event_is_flagged_in_its_inbox_copy_and_erased_once_handled()
    {
        var pingId = Guid.CreateVersion7();
        var integrationEvent = new PingSecretSentIntegrationEvent(
            TestAppFixture.TestStartedOn,
            pingId,
            "published-s3cret");

        await App.PublishAsync(integrationEvent);
        var flagBeforeQueues = await ReadInboxFlagAsync(integrationEvent.Id);
        var deliveredMessage = await ReadSingleQueueRowAsync(PongsSchema, InboxMessage.Table);
        await App.ProcessQueuesAsync();
        var processedMessage = await ReadSingleQueueRowAsync(PongsSchema, InboxMessage.Table);

        flagBeforeQueues.ShouldBeTrue();
        deliveredMessage.Payload.ShouldContain("published-s3cret");
        processedMessage.IsProcessed.ShouldBeTrue();
        processedMessage.Payload.ShouldBe("{}");
        PongTestHooks.ReceivedSecrets.ShouldContain("published-s3cret");
    }

    [Fact]
    public async Task Publishing_an_event_no_module_subscribes_to_is_refused()
    {
        var integrationEvent = new PingTouchedIntegrationEvent(App.Time.GetUtcNow(), Guid.CreateVersion7(), 1);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => App.PublishAsync(integrationEvent));

        exception.Message.ShouldBe(
            "No module subscribes to pings.ping-touched "
            + $"({typeof(PingTouchedIntegrationEvent).FullName}); "
            + "a module subscribes with an IIntegrationEventHandler<PingTouchedIntegrationEvent>");
    }

    [Fact]
    public async Task Dispatched_domain_event_runs_its_handler_and_commits_what_it_wrote()
    {
        var pingId = Guid.CreateVersion7();
        var domainEvent = new PingCreatedDomainEvent(
            App.Time.GetUtcNow(),
            TypedIdFactory<PingId>.Create(pingId),
            ValueObjectFactory<PingLabel, String>.Create("dispatched"));

        var dispatched = await App.DispatchAsync<IPingsModule>(domainEvent);
        var outboxMessage = await ReadSingleQueueRowAsync(PingsSchema, OutboxMessage.Table);
        var handledBeforeQueues = CountHandled(pingId);
        await App.ProcessQueuesAsync();
        var handledAfterQueues = CountHandled(pingId);

        dispatched.ShouldBe(Result.Success());
        outboxMessage.Type.ShouldBe("pings.ping-created");
        outboxMessage.IsProcessed.ShouldBeFalse();
        outboxMessage.Payload.ShouldContain($"\"pingId\": \"{pingId}\"");
        handledBeforeQueues.ShouldBe(0);
        handledAfterQueues.ShouldBe(1);
    }

    [Fact]
    public async Task Dispatching_an_event_the_module_does_not_handle_is_refused()
    {
        var domainEvent = new PingCreatedDomainEvent(
            App.Time.GetUtcNow(),
            TypedIdFactory<PingId>.Create(Guid.CreateVersion7()),
            ValueObjectFactory<PingLabel, String>.Create("misdirected"));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => App.DispatchAsync<IPongsModule>(domainEvent));

        exception.Message.ShouldBe(
            "No handler in IPongsModule handles PingCreatedDomainEvent "
            + $"({typeof(PingCreatedDomainEvent).FullName}); "
            + "a module handles a domain event with an IDomainEventHandler<PingCreatedDomainEvent> "
            + "in its own namespace");
    }

    [Fact]
    public async Task Enqueued_internal_command_runs_when_the_queues_run()
    {
        var pingId = await CreatePingAsync("enqueued");
        var internalCommand = new TouchPingInternalCommand(Guid.CreateVersion7(App.Time.GetUtcNow()), pingId);

        await App.EnqueueAsync<IPingsModule>(internalCommand);
        var enqueuedMessage = await ReadSingleQueueRowAsync(PingsSchema, InternalCommandMessage.Table);
        await App.ProcessQueuesAsync();
        var ping = await PingsModule.ExecuteQueryAsync(new GetPingQuery(pingId), CancellationToken);

        enqueuedMessage.ShouldBe(
            new QueueRow(
                internalCommand.Id,
                "touch-ping",
                Attempts: 0,
                IsProcessed: false,
                HasFailed: false,
                NextAttemptOn: TestAppFixture.TestStartedOn.UtcDateTime,
                Payload: $$"""{"id": "{{internalCommand.Id}}", "pingId": "{{pingId}}"}"""));

        ping.ShouldBe(
            Result<PingDto>.Success(
                new PingDto(pingId, "enqueued", Optional<String>.None(), Touches: 1, Version: 2)));
    }

    private static String BuildPingCreatedPayload(Guid messageId, Guid pingId, String label) =>
        $$"""{"id": "{{messageId}}", "label": "{{label}}", "pingId": "{{pingId}}","""
        + """ "creatorId": null, "occurredOn": "2026-09-28T08:00:00+00:00"}""";

    private async Task<Guid> CreatePingAsync(String label)
    {
        var pingId = Guid.CreateVersion7();
        var created = await PingsModule.ExecuteCommandAsync(
            new CreatePingCommand(pingId, label, Optional<String>.None()),
            CancellationToken);

        created.ShouldBe(Result<Guid>.Success(pingId));

        return pingId;
    }

    private Task<Boolean> ReadInboxFlagAsync(Guid messageId) =>
        App.Database.ExecuteScalarAsync<Boolean>(
            $"SELECT contains_secrets FROM {PongsSchema}.{InboxMessage.Table} WHERE id = @messageId",
            new { messageId });

    private async Task<QueueRow> ReadSingleQueueRowAsync(String schema, String table)
    {
        var queueRows = await App.Database.ReadQueueRowsAsync(schema, table);

        return queueRows.ShouldHaveSingleItem();
    }

    private Int32 CountHandled(Guid pingId) =>
        PongTestHooks.ReceivedPingIds.Count(receivedPingId => receivedPingId == pingId);
}
