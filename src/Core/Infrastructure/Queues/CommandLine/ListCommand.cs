using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Database;
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
        var lines = new List<String>();
        foreach (var module in GetModulesByName(serviceProvider))
        {
            await ReadFailedMessagesAsync(module, serviceProvider, lines, cancellationToken);
        }

        await outputTextWriter.WriteLineAsync(Header);
        foreach (var line in lines)
        {
            await outputTextWriter.WriteLineAsync(line);
        }

        await outputTextWriter.FlushAsync(cancellationToken);
        return Result.Success();
    }

    private static IEnumerable<ModuleDescriptor> GetModulesByName(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<ModuleRegistry>().Modules
            .OrderBy(module => module.Name, StringComparer.Ordinal);

    private static async Task ReadFailedMessagesAsync(
        ModuleDescriptor module,
        IServiceProvider serviceProvider,
        List<String> lines,
        CancellationToken cancellationToken)
    {
        await using var connection = await ModuleConnection.OpenAsync(
            module,
            serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(module.Key),
            cancellationToken);

        foreach (var queue in QueueTables.Names)
        {
            var failedMessages = await connection.QueryAsync<FailedMessageRow>(
                new CommandDefinition(BuildSql(module.Schema, queue), cancellationToken: cancellationToken));

            lines.AddRange(failedMessages.Select(failedMessage => failedMessage.BuildLine(module.Name, queue)));
        }
    }

    private static String BuildSql(String schema, String queue) =>
        $"""
        SELECT id AS "Id", type AS "Type", attempts AS "Attempts", failed_on AS "FailedOn", last_error AS "LastError"
        FROM "{schema}".{queue}
        WHERE failed_on IS NOT NULL
        ORDER BY failed_on, id
        """;
}
