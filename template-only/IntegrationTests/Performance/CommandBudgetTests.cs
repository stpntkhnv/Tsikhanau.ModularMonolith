using System.Diagnostics;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.IntegrationTests.Diagnostics;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings;

namespace Tsikhanau.Monolith.IntegrationTests.Performance;

public sealed partial class CommandBudgetTests(PostgresFixture postgresFixture)
{
    private const Int32 WarmupCommands = 10;
    private const Int32 PingEventCount = 100;
    private const Int32 MaxCreatePingCommandExecutions = 1;
    private const Int32 MaxDrainCommandExecutions = 656;
    private const String ExtraCommandSql = "SELECT 1";
    private const String CreatePingSqlStart = "INSERT INTO pings.outbox_messages";

    private static readonly ActivitySource MeasuredActivitySource =
        new("Tsikhanau.Monolith.IntegrationTests.CommandBudget");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern { get; }

    [Fact]
    public async Task Create_ping_runs_at_most_1_command_execution()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture, writesLogs: false);
        using var npgsqlCommandRecorder = new NpgsqlCommandRecorder(MeasuredActivitySource.Name);
        await CreatePingsAsync(testModulesHost, "warmup", WarmupCommands);

        var commands = await RecordCommandsAsync(
            npgsqlCommandRecorder,
            () => testModulesHost.CreatePingAsync("measured"));

        var commandReport = BuildCommandReport(commands);

        commands.ShouldNotBeEmpty(commandReport);
        commands.Count.ShouldBeLessThanOrEqualTo(MaxCreatePingCommandExecutions, commandReport);
    }

    [Fact]
    public async Task Draining_100_ping_events_runs_at_most_656_command_executions()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture, writesLogs: false);
        using var npgsqlCommandRecorder = new NpgsqlCommandRecorder(MeasuredActivitySource.Name);
        await CreatePingsAsync(testModulesHost, "budget", PingEventCount);

        var commands = await RecordCommandsAsync(npgsqlCommandRecorder, testModulesHost.ProcessQueuesAsync);
        var pongCount = await testModulesHost.CountPongsAsync();
        var commandReport = BuildCommandReport(commands);

        pongCount.ShouldBe(PingEventCount);
        commands.ShouldNotBeEmpty(commandReport);
        commands.Count.ShouldBeLessThanOrEqualTo(MaxDrainCommandExecutions, commandReport);
    }

    [Fact]
    public async Task An_extra_command_inside_the_measured_call_is_counted()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture, writesLogs: false);
        using var npgsqlCommandRecorder = new NpgsqlCommandRecorder(MeasuredActivitySource.Name);
        var sqlConnectionFactory = testModulesHost.Services.GetRequiredService<ISqlConnectionFactory<IPingsModule>>();
        await CreatePingsAsync(testModulesHost, "warmup", WarmupCommands);

        var commands = await RecordCommandsAsync(
            npgsqlCommandRecorder,
            async () =>
            {
                await testModulesHost.CreatePingAsync("measured");
                await RunExtraCommandAsync(sqlConnectionFactory);
            });

        var commandReport = BuildCommandReport(commands);

        commands.Count.ShouldBe(MaxCreatePingCommandExecutions + 1, commandReport);
        commands.ShouldContain(command => command.Text == ExtraCommandSql, commandReport);
    }

    [Fact]
    public async Task A_connection_open_inside_the_measured_call_is_not_counted()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture, writesLogs: false);
        using var npgsqlCommandRecorder = new NpgsqlCommandRecorder(MeasuredActivitySource.Name);
        await CreatePingsAsync(testModulesHost, "warmup", WarmupCommands);
        testModulesHost.Services
            .GetRequiredKeyedService<NpgsqlDataSource>(typeof(IPingsModule))
            .Clear();

        var commands = await RecordCommandsAsync(
            npgsqlCommandRecorder,
            () => testModulesHost.CreatePingAsync("measured"));

        var commandReport = BuildCommandReport(commands);

        commands.Count.ShouldBe(MaxCreatePingCommandExecutions, commandReport);
        commands.ShouldAllBe(
            command => command.Text.StartsWith(CreatePingSqlStart, StringComparison.Ordinal),
            commandReport);
    }

    private static async Task CreatePingsAsync(TestModulesHost testModulesHost, String labelPrefix, Int32 count)
    {
        for (var index = 0; index < count; index++)
        {
            await testModulesHost.CreatePingAsync($"{labelPrefix}-{index}");
        }
    }

    private static async Task<IReadOnlyList<RecordedCommand>> RecordCommandsAsync(
        NpgsqlCommandRecorder npgsqlCommandRecorder,
        Func<Task> measuredCallAsync)
    {
        npgsqlCommandRecorder.Start();
        using (var measuredActivity = MeasuredActivitySource.StartActivity("measured call"))
        {
            measuredActivity.ShouldNotBeNull();
            await measuredCallAsync();
        }

        npgsqlCommandRecorder.Stop();
        return npgsqlCommandRecorder.Commands;
    }

    private static async Task RunExtraCommandAsync(ISqlConnectionFactory<IPingsModule> sqlConnectionFactory)
    {
        await using var connection = await sqlConnectionFactory.OpenConnectionAsync(CancellationToken);
        await connection.ExecuteScalarAsync<Int32>(
            new CommandDefinition(ExtraCommandSql, cancellationToken: CancellationToken));
    }

    private static String BuildCommandReport(IReadOnlyList<RecordedCommand> commands) =>
        String.Join(Environment.NewLine, commands.Select(command => WhitespacePattern.Replace(command.Text, " ")));
}
