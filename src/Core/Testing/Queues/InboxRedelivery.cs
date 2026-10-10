using Dapper;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Railway;
using Xunit;

namespace Tsikhanau.Monolith.Testing.Queues;

internal static class InboxRedelivery
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task RedeliverAsync(
        NpgsqlDataSource npgsqlDataSource,
        ModuleDescriptor module,
        Guid messageId,
        DateTimeOffset redeliveredOn)
    {
        await using var connection = await npgsqlDataSource.OpenConnectionAsync(CancellationToken);
        var foundMessage = await ReadMessageAsync(connection, module, messageId);
        EnsureRedeliverable(module, messageId, foundMessage);
        await connection.ExecuteAsync(
            new CommandDefinition(
                BuildRedeliverSql(module),
                new { messageId, redeliveredOn },
                cancellationToken: CancellationToken));
    }

    private static async Task<Optional<InboxMessageState>> ReadMessageAsync(
        NpgsqlConnection connection,
        ModuleDescriptor module,
        Guid messageId)
    {
        var messages = await connection.QueryAsync<InboxMessageState>(
            new CommandDefinition(BuildReadSql(module), new { messageId }, cancellationToken: CancellationToken));

        return messages.SingleOrDefault();
    }

    private static void EnsureRedeliverable(
        ModuleDescriptor module,
        Guid messageId,
        Optional<InboxMessageState> foundMessage)
    {
        if (foundMessage.IsNone)
        {
            throw new InvalidOperationException($"Module {module.Name} has no inbox message {messageId}");
        }

        var foundRefusal = FindRefusal(foundMessage.Value);
        if (foundRefusal.HasValue)
        {
            throw new InvalidOperationException(
                $"Inbox message {messageId} of module {module.Name} {foundRefusal.Value}");
        }
    }

    private static Optional<String> FindRefusal(InboxMessageState message) =>
        message switch
        {
            { HasFailed: true } =>
                $"failed ({message.LastError}); only a processed message can be delivered again",
            { IsProcessed: false } =>
                "is not processed yet; process the queues before delivering it again",
            { HasErasedPayload: true } =>
                $"({message.Type}) carried secrets that were erased once it was processed; "
                + "it cannot be delivered again",
            _ => Optional<String>.None(),
        };

    private static String BuildReadSql(ModuleDescriptor module) =>
        $"""
        SELECT type AS "Type", payload::text AS "Payload", processed_on IS NOT NULL AS "IsProcessed",
               failed_on IS NOT NULL AS "HasFailed", coalesce(last_error, '') AS "LastError"
        FROM "{module.Schema}".{InboxMessage.Table}
        WHERE id = @messageId
        """;

    private static String BuildRedeliverSql(ModuleDescriptor module) =>
        $"""
        UPDATE "{module.Schema}".{InboxMessage.Table}
        SET processed_on = NULL, attempts = 0, last_error = NULL, next_attempt_on = @redeliveredOn
        WHERE id = @messageId
        """;
}
