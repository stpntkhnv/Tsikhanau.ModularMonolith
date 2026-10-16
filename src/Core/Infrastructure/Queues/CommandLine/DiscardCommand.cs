using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Dapper;
using Npgsql;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal sealed class DiscardCommand(MessageAddress message) : IQueuesCommand
{
    private const String TypeColumn = "type";

    private const String PayloadColumn = "payload";

    private const String ContainsSecretsColumn = "contains_secrets";

    private const String LastErrorColumn = "last_error";

    private static readonly JsonElement ErasedPayload = JsonElement.Parse(QueueMessage.ErasedPayload);

    public async Task<Result<Unit>> RunAsync(
        IServiceProvider serviceProvider,
        TextWriter outputTextWriter,
        CancellationToken cancellationToken)
    {
        var secretRowRule = serviceProvider.ResolveSecretRowRule(message.Module, message.Queue);
        await using var connection = await message.OpenConnectionAsync(serviceProvider, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return await DeleteAsync(connection, transaction, secretRowRule, cancellationToken)
            .MatchAsync(
                deletedRow => WriteAndCommitAsync(deletedRow, outputTextWriter, transaction, cancellationToken),
                () => DescribeConflictAsync(connection, transaction, cancellationToken));
    }

    private static async Task<Result<Unit>> WriteAndCommitAsync(
        String deletedRow,
        TextWriter outputTextWriter,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await outputTextWriter.WriteLineAsync(deletedRow);
        await outputTextWriter.FlushAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private static String SerializeRow(DbDataReader reader, ISecretRowRule secretRowRule)
    {
        var isSecret = secretRowRule.IsSecret(
            reader.GetFieldValue<String>(reader.GetOrdinal(TypeColumn)),
            reader.GetFieldValue<Boolean>(reader.GetOrdinal(ContainsSecretsColumn)));

        var row = Enumerable
            .Range(0, reader.FieldCount)
            .ToDictionary(reader.GetName, ordinal => ReadValue(reader, ordinal, isSecret));

        return JsonSerializer.Serialize(row);
    }

    private static Object? ReadValue(DbDataReader reader, Int32 ordinal, Boolean isSecret) =>
        (reader.GetName(ordinal), reader.GetValue(ordinal)) switch
        {
            (_, DBNull) => null,
            (PayloadColumn, _) when isSecret => ErasedPayload,
            (PayloadColumn, String payload) => JsonElement.Parse(payload),
            (LastErrorColumn, String lastError) when isSecret => HandlingFailure
                .FindCode(lastError, isSecret)
                .GetValueOrDefault(),
            (_, DateTime timestamp) => timestamp.ToString("O", CultureInfo.InvariantCulture),
            (_, var value) => value,
        };

    private async Task<Optional<String>> DeleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ISecretRowRule secretRowRule,
        CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(
                BuildDeleteSql(),
                new { message.MessageId },
                transaction,
                cancellationToken: cancellationToken));

        return await reader.ReadAsync(cancellationToken)
            ? SerializeRow(reader, secretRowRule)
            : Optional<String>.None();
    }

    private async Task<Result<Unit>> DescribeConflictAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<Boolean>(
            new CommandDefinition(
                BuildExistsSql(),
                new { message.MessageId },
                transaction,
                cancellationToken: cancellationToken));

        return exists
            ? MessageConflicts.NotFailed(message)
            : MessageConflicts.Missing(message);
    }

    private String BuildDeleteSql() =>
        $"DELETE FROM {message.Table} WHERE id = @MessageId AND failed_on IS NOT NULL RETURNING *";

    private String BuildExistsSql() =>
        $"SELECT EXISTS (SELECT FROM {message.Table} WHERE id = @MessageId)";
}
