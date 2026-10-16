using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal sealed class RetryCommand(MessageAddress message) : IQueuesCommand
{
    public async Task<Result<Unit>> RunAsync(
        IServiceProvider serviceProvider,
        TextWriter outputTextWriter,
        CancellationToken cancellationToken)
    {
        var retriedOn = serviceProvider
            .GetRequiredService<TimeProvider>()
            .GetUtcNow();

        await using var connection = await message.OpenConnectionAsync(serviceProvider, cancellationToken);
        var retriedCount = await connection.ExecuteAsync(
            new CommandDefinition(
                BuildRetrySql(),
                new { message.MessageId, RetriedOn = retriedOn, QueueMessage.ErasedPayload },
                cancellationToken: cancellationToken));

        return retriedCount > 0
            ? Result.Success()
            : await DescribeConflictAsync(connection, cancellationToken);
    }

    private async Task<Error> DescribeConflictAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var hasFailedWithErasedPayload = await connection.ExecuteScalarAsync<Boolean?>(
            new CommandDefinition(
                BuildErasureSql(),
                new { message.MessageId, QueueMessage.ErasedPayload },
                cancellationToken: cancellationToken));

        return hasFailedWithErasedPayload switch
        {
            null => MessageConflicts.Missing(message),
            true => MessageConflicts.ErasedPayload(message),
            false => MessageConflicts.NotFailed(message),
        };
    }

    private String BuildRetrySql() =>
        $"""
        UPDATE {message.Table}
        SET attempts = 0, failed_on = NULL, next_attempt_on = @RetriedOn
        WHERE id = @MessageId AND failed_on IS NOT NULL AND payload <> @ErasedPayload::jsonb
        """;

    private String BuildErasureSql() =>
        $"""
        SELECT failed_on IS NOT NULL AND payload = @ErasedPayload::jsonb
        FROM {message.Table}
        WHERE id = @MessageId
        """;
}
