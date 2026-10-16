using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal sealed class ListCommand : IQueuesCommand
{
    private const String Header = "module\tqueue\tid\ttype\tattempts\tfailed_on\tcode";

    public async Task<Result<Unit>> RunAsync(
        IServiceProvider serviceProvider,
        TextWriter outputTextWriter,
        CancellationToken cancellationToken)
    {
        await outputTextWriter.WriteLineAsync(Header);
        foreach (var module in GetModulesByName(serviceProvider))
        {
            await WriteFailedMessagesAsync(module, serviceProvider, outputTextWriter, cancellationToken);
        }

        await outputTextWriter.FlushAsync(cancellationToken);
        return Result.Success();
    }

    private static IEnumerable<ModuleDescriptor> GetModulesByName(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<ModuleRegistry>().Modules
            .OrderBy(module => module.Name, StringComparer.Ordinal);

    private static async Task WriteFailedMessagesAsync(
        ModuleDescriptor module,
        IServiceProvider serviceProvider,
        TextWriter outputTextWriter,
        CancellationToken cancellationToken)
    {
        await using var connection = await serviceProvider
            .GetRequiredKeyedService<NpgsqlDataSource>(module.Key)
            .OpenConnectionAsync(cancellationToken);

        foreach (var queue in QueueTables.Names)
        {
            var failedMessages = await connection.QueryAsync<FailedMessageRow>(
                new CommandDefinition(BuildSql(module.Schema, queue), cancellationToken: cancellationToken));

            foreach (var failedMessage in failedMessages)
            {
                await outputTextWriter.WriteLineAsync(failedMessage.BuildLine(module.Name, queue));
            }
        }
    }

    private static String BuildSql(String schema, String queue) =>
        $"""
        SELECT id AS "Id", type AS "Type", attempts AS "Attempts", failed_on AS "FailedOn",
               coalesce(split_part(last_error, ': ', 1), '') AS "Code"
        FROM "{schema}".{queue}
        WHERE failed_on IS NOT NULL
        ORDER BY failed_on, id
        """;
}
