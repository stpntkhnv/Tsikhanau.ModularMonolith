using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.Database;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;
using Tsikhanau.Monolith.TestModules.Pings.Features.SendSecret;
using Tsikhanau.Monolith.TestModules.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Features;
using Tsikhanau.Monolith.TestModules.Pongs.Features.CountPongs;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.App;

[Collection(TestAppCollection.Name)]
public sealed class InboxRedeliveryTests(TestAppFixture testAppFixture) : TestAppTest(testAppFixture)
{
    private const String Pongs = "Pongs";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private IPingsModule PingsModule => App.Services.GetRequiredService<IPingsModule>();

    private IPongsModule PongsModule => App.Services.GetRequiredService<IPongsModule>();

    private PongTestHooks PongTestHooks => App.Services.GetRequiredService<PongTestHooks>();

    private String PongsSchema => App.GetModule(Pongs).Schema;

    [Fact]
    public async Task Redelivered_inbox_message_runs_its_handler_a_second_time()
    {
        var pingId = await CreatePingAsync("redelivered");
        await App.ProcessQueuesAsync();
        var processedMessage = await ReadSingleInboxMessageAsync();
        var handledBefore = CountHandled(pingId);
        var redeliveredOn = App.Time.GetUtcNow();

        await App.RedeliverInboxAsync(Pongs, processedMessage.Id);
        var redeliveredMessage = await ReadSingleInboxMessageAsync();
        await App.ProcessQueuesAsync();
        var handledAfter = CountHandled(pingId);
        var reprocessedMessage = await ReadSingleInboxMessageAsync();
        var pongCount = await PongsModule.ExecuteQueryAsync(new CountPongsQuery(), CancellationToken);

        handledBefore.ShouldBe(1);
        redeliveredMessage.ShouldBe(
            processedMessage with
            {
                Attempts = 0,
                IsProcessed = false,
                NextAttemptOn = redeliveredOn.UtcDateTime,
            });

        handledAfter.ShouldBe(2);
        reprocessedMessage.ShouldBe(processedMessage);
        pongCount.ShouldBe(Result<Int32>.Success(1));
    }

    [Fact]
    public async Task Redelivery_delivers_only_the_named_message_again()
    {
        var targetEvent = CreatePingCreatedEvent("redelivery-target");
        var bystanderEvent = CreatePingCreatedEvent("redelivery-bystander");
        await App.PublishAsync(targetEvent);
        await App.PublishAsync(bystanderEvent);
        await App.ProcessQueuesAsync();
        var processedMessages = await App.Database.ReadQueueRowsAsync(PongsSchema, InboxMessage.Table);
        var targetMessage = processedMessages.Single(message => message.Id == targetEvent.Id);
        var bystanderMessage = processedMessages.Single(message => message.Id == bystanderEvent.Id);
        var redeliveredOn = App.Time.GetUtcNow();

        await App.RedeliverInboxAsync(Pongs, targetEvent.Id);
        var redeliveredMessages = await App.Database.ReadQueueRowsAsync(PongsSchema, InboxMessage.Table);
        await App.ProcessQueuesAsync();
        var targetHandled = CountHandled(targetEvent.PingId);
        var bystanderHandled = CountHandled(bystanderEvent.PingId);

        redeliveredMessages.ShouldBe(
            [
                targetMessage with { Attempts = 0, IsProcessed = false, NextAttemptOn = redeliveredOn.UtcDateTime },
                bystanderMessage,
            ],
            ignoreOrder: true);

        targetHandled.ShouldBe(2);
        bystanderHandled.ShouldBe(1);
    }

    [Fact]
    public async Task Redelivery_clears_attempts_and_last_error_and_makes_the_message_due_at_the_current_time()
    {
        const String label = "retried-then-redelivered";
        PongTestHooks.FailUnexpectedlyFor = PongTestHooks.FailUnexpectedlyFor.Add(label);
        await CreatePingAsync(label);
        await App.ProcessQueuesAllowingFailuresAsync();
        PongTestHooks.FailUnexpectedlyFor = PongTestHooks.FailUnexpectedlyFor.Remove(label);
        App.Time.Advance(TimeSpan.FromSeconds(1));
        await App.ProcessQueuesAsync();
        var processedMessage = await ReadSingleInboxMessageAsync();
        var processedState = await ReadDeliveryStateAsync(processedMessage.Id);
        var redeliveredOn = App.Time.GetUtcNow();

        await App.RedeliverInboxAsync(Pongs, processedMessage.Id);
        var redeliveredState = await ReadDeliveryStateAsync(processedMessage.Id);

        processedState.Attempts.ShouldBe(2);
        processedState.LastError.ShouldBe("Pong.Broken: Pong broke on this ping");
        redeliveredState.ShouldBe(
            new DeliveryState(
                Attempts: 0,
                IsProcessed: false,
                NextAttemptOn: redeliveredOn.UtcDateTime,
                LastError: ""));
    }

    [Fact]
    public async Task Redelivery_refuses_an_unknown_message()
    {
        var messageId = Guid.CreateVersion7();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => App.RedeliverInboxAsync(Pongs, messageId));

        exception.Message.ShouldBe($"Module Pongs has no inbox message {messageId}");
    }

    [Fact]
    public async Task Redelivery_refuses_a_message_that_is_not_processed_yet()
    {
        await App.PublishAsync(CreatePingCreatedEvent("waiting"));
        var deliveredMessage = await ReadSingleInboxMessageAsync();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => App.RedeliverInboxAsync(Pongs, deliveredMessage.Id));

        var messageAfterRefusal = await ReadSingleInboxMessageAsync();

        exception.Message.ShouldBe(
            $"Inbox message {deliveredMessage.Id} of module Pongs is not processed yet; "
            + "process the queues before delivering it again");

        messageAfterRefusal.ShouldBe(deliveredMessage);
    }

    [Fact]
    public async Task Redelivery_refuses_a_failed_message()
    {
        const String label = "refused-before-redelivery";
        PongTestHooks.FailFor = PongTestHooks.FailFor.Add(label);
        await CreatePingAsync(label);
        await App.ProcessQueuesAllowingFailuresAsync();
        var failedMessage = await ReadSingleInboxMessageAsync();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => App.RedeliverInboxAsync(Pongs, failedMessage.Id));

        var messageAfterRefusal = await ReadSingleInboxMessageAsync();

        exception.Message.ShouldBe(
            $"Inbox message {failedMessage.Id} of module Pongs failed (Pong.Rejected: Pong refused this ping); "
            + "only a processed message can be delivered again");

        messageAfterRefusal.ShouldBe(failedMessage);
    }

    [Fact]
    public async Task Redelivery_refuses_a_message_whose_secret_was_erased()
    {
        var sent = await PingsModule.ExecuteCommandAsync(
            new SendSecretCommand(Guid.CreateVersion7(), "one-time"),
            CancellationToken);

        await App.ProcessQueuesAsync();
        var processedMessage = await ReadSingleInboxMessageAsync();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => App.RedeliverInboxAsync(Pongs, processedMessage.Id));

        var messageAfterRefusal = await ReadSingleInboxMessageAsync();

        sent.ShouldBe(Result.Success());
        exception.Message.ShouldBe(
            $"Inbox message {processedMessage.Id} of module Pongs (pings.ping-secret-sent) carried secrets "
            + "that were erased once it was processed; it cannot be delivered again");

        messageAfterRefusal.ShouldBe(processedMessage);
    }

    private async Task<Guid> CreatePingAsync(String label)
    {
        var pingId = Guid.CreateVersion7();
        var created = await PingsModule.ExecuteCommandAsync(
            new CreatePingCommand(pingId, label, Optional<String>.None()),
            CancellationToken);

        created.ShouldBe(Result<Guid>.Success(pingId));

        return pingId;
    }

    private PingCreatedIntegrationEvent CreatePingCreatedEvent(String label) =>
        new(App.Time.GetUtcNow(), Guid.CreateVersion7(), label, Optional<String>.None());

    private async Task<QueueRow> ReadSingleInboxMessageAsync()
    {
        var inboxMessages = await App.Database.ReadQueueRowsAsync(PongsSchema, InboxMessage.Table);

        return inboxMessages.ShouldHaveSingleItem();
    }

    private async Task<DeliveryState> ReadDeliveryStateAsync(Guid messageId)
    {
        var deliveryStates = await App.Database.QueryAsync<DeliveryState>(
            $"""
            SELECT attempts AS "Attempts", processed_on IS NOT NULL AS "IsProcessed",
                   next_attempt_on AS "NextAttemptOn", coalesce(last_error, '') AS "LastError"
            FROM "{PongsSchema}".{InboxMessage.Table}
            WHERE id = @messageId
            """,
            new { messageId });

        return deliveryStates.ShouldHaveSingleItem();
    }

    private Int32 CountHandled(Guid pingId) =>
        PongTestHooks.ReceivedPingIds.Count(receivedPingId => receivedPingId == pingId);

    private sealed record DeliveryState(Int32 Attempts, Boolean IsProcessed, DateTime NextAttemptOn, String LastError);
}
