using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Queues.Messages;
using Xunit;

namespace Tsikhanau.Monolith.Testing.Queues;

public static class UnsettledMessages
{
    public static async Task<IReadOnlyList<UnsettledMessage>> ReadAsync(IServiceProvider serviceProvider)
    {
        var unsettledMessages = new List<UnsettledMessage>();
        foreach (var module in serviceProvider.GetRequiredService<ModuleRegistry>().Modules)
        {
            unsettledMessages.AddRange(await ReadAsync(serviceProvider, module));
        }

        return unsettledMessages;
    }

    private static async Task<IEnumerable<UnsettledMessage>> ReadAsync(
        IServiceProvider serviceProvider,
        ModuleDescriptor module)
    {
        await using var connection = await serviceProvider
            .ResolveDataSource(module)
            .OpenConnectionAsync(TestContext.Current.CancellationToken);

        return await connection.QueryAsync<UnsettledMessage>(
            new CommandDefinition(
                BuildSql(module),
                cancellationToken: TestContext.Current.CancellationToken));
    }

    private static String BuildSql(ModuleDescriptor module) =>
        String.Join(
            "\nUNION ALL\n",
            QueueTables.Names.Select(table => $"""
                SELECT '{module.Name}' AS "Module", '{table}' AS "Table", type AS "Type",
                       attempts AS "Attempts", coalesce(last_error, '') AS "LastError"
                FROM "{module.Schema}".{table}
                WHERE failed_on IS NOT NULL OR (processed_on IS NULL AND attempts > 0)
                """));
}
