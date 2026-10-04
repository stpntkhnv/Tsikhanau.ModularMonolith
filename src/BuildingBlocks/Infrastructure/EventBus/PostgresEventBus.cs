using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ModularMonolith.BuildingBlocks.Infrastructure.Inbox;
using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace ModularMonolith.BuildingBlocks.Infrastructure.EventBus;

public sealed class PostgresEventBus(SubscriptionRegistry subscriptionRegistry, TimeProvider timeProvider) : IEventBus
{
    public async Task DeliverAsync(
        OutboxMessage message,
        ModuleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var receivedOn = timeProvider.GetUtcNow();
        var traceParent = Activity.Current?.Id ?? message.TraceParent;

        foreach (var subscriber in subscriptionRegistry.GetSubscribers(message.Type))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                BuildInboxInsertSql(subscriber.Schema),
                CreateInboxInsertParameters(message, traceParent, receivedOn),
                cancellationToken);
        }
    }

    private static String BuildInboxInsertSql(String schema) =>
        $$"""
        INSERT INTO "{{schema}}".{{InboxMessage.Table}}
            (id, type, payload, trace_parent, attempts, next_attempt_on, occurred_on, received_on)
        VALUES ({0}, {1}, {2}::jsonb, {3}, 0, {4}, {5}, {4})
        ON CONFLICT (id) DO NOTHING
        """;

    private static Object[] CreateInboxInsertParameters(
        OutboxMessage message,
        String? traceParent,
        DateTimeOffset receivedOn) =>
        [
            message.Id,
            message.Type,
            message.Payload,
            CreateNullableTextParameter(traceParent),
            receivedOn,
            message.OccurredOn,
        ];

    private static NpgsqlParameter CreateNullableTextParameter(String? value) =>
        new() { NpgsqlDbType = NpgsqlDbType.Text, Value = (Object?)value ?? DBNull.Value };
}
