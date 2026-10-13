using System.Globalization;

namespace Tsikhanau.Monolith.Analyzers.Tests;

public sealed class SqlConstantAnalyzerTests
{
    private const Int32 SinkLine = 34;

    private const String InterpolationWithVariableHoleMessage =
        "SQL passed to Dapper or EF Core raw SQL must be a constant: put values in parameters (@Name), never into the "
        + "text. Rule in AGENTS.md: Module SQL names its columns and never another module's schema; module code opens "
        + "no database connection of its own. SQL passed to Dapper or EF Core raw SQL is a constant, values go in "
        + "parameters. Playbook: docs/playbooks/add-feature.md";

    private static readonly String[] Sinks =
    [
        "_ = connection.QueryAsync<Note>({0});",
        "_ = connection.ExecuteAsync({0});",
        "_ = SqlMapper.QueryAsync(connection, {0});",
        "_ = new CommandDefinition({0});",
        "_ = new CommandDefinition(commandText: {0}, parameters: parameters);",
        "_ = new CommandDefinition(parameters: parameters, commandText: {0});",
        "CommandDefinition command = new({0});",
        "_ = new D({0});",
        "_ = database.ExecuteSqlRawAsync({0});",
        "_ = database.ExecuteSqlRaw({0});",
        "_ = database.SqlQueryRaw<Int32>({0});",
        "_ = notes.FromSqlRaw({0});",
    ];

    public static MatrixTheoryData<String, String> SqlBuiltAtRunTime =>
        new(
            Sinks,
            [
                "query",
                "sql",
                "$\"SELECT id FROM notes.notes WHERE id = {id}\"",
                "\"SELECT id FROM notes.notes WHERE id = \" + id",
                "ReadSql()",
                "StaticSql",
                "flag ? Sql : Columns",
            ]);

    public static MatrixTheoryData<String, String> ConstantSql =>
        new(
            Sinks,
            [
                "\"SELECT 1\"",
                "Sql",
                "\"SELECT id \" + \"FROM notes.notes\"",
                "$\"{Columns} FROM notes.notes WHERE id = @Id\"",
            ]);

    [Fact]
    public async Task Diagnostic_states_the_fix_the_rule_line_and_the_playbook()
    {
        var messages = await AnalyzerRunner.GetReportedMessagesOfValidSourceAsync(
            new SqlConstantAnalyzer(),
            """
            using System;
            using System.Collections.Generic;
            using System.Data;
            using System.Threading.Tasks;
            using Dapper;

            namespace App.Modules.Notes.Features.GetNote;

            sealed class GetNoteQueryHandler
            {
                Task<IEnumerable<Int32>> ReadAsync(IDbConnection connection, Int32 id) =>
                    connection.QueryAsync<Int32>($"SELECT id FROM notes.notes WHERE id = {id}");
            }
            """);

        messages.ShouldBe([InterpolationWithVariableHoleMessage]);
    }

    [Theory]
    [MemberData(nameof(SqlBuiltAtRunTime))]
    public async Task Sql_built_at_run_time_is_reported_where_it_is_passed(String sink, String sql)
    {
        var lines = await GetReportedLinesAsync(sink, sql);

        lines.ShouldBe([SinkLine]);
    }

    [Theory]
    [MemberData(nameof(ConstantSql))]
    public async Task Constant_sql_passes(String sink, String sql)
    {
        var lines = await GetReportedLinesAsync(sink, sql);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Method_of_another_type_named_like_a_dapper_method_passes()
    {
        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourceAsync(
            new SqlConstantAnalyzer(),
            """
            using System;
            using System.Threading.Tasks;

            namespace App.Modules.Notes.Features.GetNote;

            sealed class NoteSearch
            {
                public Task<Int32> QueryAsync(String sql) => Task.FromResult(sql.Length);
            }

            sealed class GetNoteQueryHandler
            {
                Task<Int32> CountAsync(NoteSearch noteSearch, String title) =>
                    noteSearch.QueryAsync($"title = {title}");
            }
            """);

        lines.ShouldBeEmpty();
    }

    private static Task<IReadOnlyList<Int32>> GetReportedLinesAsync(String sink, String sql)
    {
        var statement = String.Format(CultureInfo.InvariantCulture, sink, sql);

        return AnalyzerRunner.GetReportedLinesOfValidSourceAsync(
            new SqlConstantAnalyzer(),
            $$"""
            using System;
            using System.Data;
            using System.Threading.Tasks;
            using Dapper;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using D = Dapper.CommandDefinition;

            namespace App.Modules.Notes.Features.GetNote;

            sealed class Note
            {
                public Int32 Id { get; set; }
            }

            sealed class NoteReader
            {
                private const String Sql = "SELECT id FROM notes.notes";

                private const String Columns = "SELECT id";

                private static readonly String StaticSql = "SELECT id FROM notes.notes";

                void Read(
                    IDbConnection connection,
                    DatabaseFacade database,
                    DbSet<Note> notes,
                    Object parameters,
                    String sql,
                    Int32 id,
                    Boolean flag)
                {
                    var query = "SELECT id FROM notes.notes";
                    {{statement}}
                }

                private static String ReadSql() => Sql;
            }
            """);
    }
}
