using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Dapper;
using Npgsql;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal sealed class DiscardCommand(MessageAddress message) : IQueuesCommand
{
    private const String PayloadColumn = "payload";

    public async Task<Result<Unit>> RunAsync(
        IServiceProvider serviceProvider,
        TextWriter outputTextWriter,
        CancellationToken cancellationToken)
    {
        await using var connection = await message.OpenConnectionAsync(serviceProvider, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return await DeleteAsync(connection, transaction, cancellationToken)
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

    private static Dictionary<String, Object?> ReadRow(DbDataReader reader) =>
        Enumerable
            .Range(0, reader.FieldCount)
            .ToDictionary(reader.GetName, ordinal => ReadValue(reader, ordinal));

    private static Object? ReadValue(DbDataReader reader, Int32 ordinal) =>
        reader.GetValue(ordinal) switch
        {
            DBNull => null,
            String payload when reader.GetName(ordinal) == PayloadColumn => JsonElement.Parse(payload),
            DateTime timestamp => timestamp.ToString("O", CultureInfo.InvariantCulture),
            var value => value,
        };

    private async Task<Optional<String>> DeleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(
                BuildDeleteSql(),
                new { message.MessageId },
                transaction,
                cancellationToken: cancellationToken));

        return await reader.ReadAsync(cancellationToken)
            ? JsonSerializer.Serialize(ReadRow(reader))
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
