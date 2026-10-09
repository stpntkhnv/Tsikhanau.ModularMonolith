using System.Reflection;
using System.Text.RegularExpressions;
using ModularMonolith.ArchTests.Support;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.Contracts.Queries;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace ModularMonolith.ArchTests.Modules;

public sealed partial class SqlTextTests
{
    private const String SchemaNameField = "SchemaName";
    private const String SampleNamespace = "ModularMonolith.ArchTests.Modules";
    private const String LiteralOwnerName = "ModularMonolith.ArchTests.Modules.SqlTextTests+LiteralOwner";
    private const String SampleQueryHandlerName = "ModularMonolith.ArchTests.Modules.SqlTextTests+SampleQueryHandler";
    private const BindingFlags ConstantFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private const String WritingStatementPattern =
        """\b(INSERT\s+INTO|UPDATE\s+[\w."]+(\s+(AS\s+)?[\w."]+)?\s+SET|DELETE\s+FROM|MERGE\s+INTO"""
            + """|TRUNCATE\s+(TABLE\s+)?(ONLY\s+)?[\w."]+)\b""";

    private const String StarColumnPattern =
        """\b(SELECT|RETURNING)\s+(DISTINCT\s+)?([\w."]+\s*,\s*)*([\w."]+\.)?\*""";

    private static ArchModule SampleModule => new("Samples", SampleNamespace, [typeof(SqlTextTests).Assembly]);

    [Theory]
    [EveryModule]
    public void Query_handlers_only_read(String name)
    {
        var queryHandlerLiterals = ReadQueryHandlerLiterals(ArchModules.Get(name));

        Assert.SkipWhen(queryHandlerLiterals.Count == 0, $"{name} has no SQL in query handlers");

        queryHandlerLiterals
            .Where(literal => WritingStatement.IsMatch(literal.Literal))
            .Select(literal => $"{literal.TypeName} writes: {literal.Literal}")
            .ShouldBeEmpty("A query handler only reads; move the change into a command");
    }

    [Theory]
    [EveryModule]
    public void Module_sql_names_no_other_module_schema(String name)
    {
        var module = ArchModules.Get(name);
        var otherSchemas = ArchModules.Names
            .Where(otherName => otherName != name)
            .Select(ReadSchema)
            .ToList();

        var literals = StringLiterals.ReadFrom(module);

        Assert.SkipWhen(literals.Count == 0, $"{name} has no string literals");

        literals
            .Where(literal => otherSchemas.Any(schema => NamesSchema(literal.Literal, schema)))
            .Select(literal => $"{literal.TypeName} reaches another module: {literal.Literal}")
            .ShouldBeEmpty("Read another module through its query facade, not its tables");
    }

    [Theory]
    [EveryModule]
    public void Module_sql_names_its_columns(String name)
    {
        var literals = StringLiterals.ReadFrom(ArchModules.Get(name));

        Assert.SkipWhen(literals.Count == 0, $"{name} has no string literals");

        literals
            .Where(literal => StarColumn.IsMatch(literal.Literal))
            .Select(literal => $"{literal.TypeName} reads every column: {literal.Literal}")
            .ShouldBeEmpty("Name the columns; a column added under a running instance breaks SELECT *");
    }

    [Fact]
    public void Literals_inside_async_state_machines_are_read()
    {
        var literals = StringLiterals.ReadFrom(ArchModules.Get("Pongs"));

        literals.ShouldContain((
            "Tsikhanau.Monolith.TestModules.Pongs.Features.CountPongs.CountPongsQueryHandler",
            "SELECT count(*)::int FROM pongs.pongs"));
    }

    [Fact]
    public void Literals_belong_to_the_nearest_handwritten_type()
    {
        var literals = StringLiterals.ReadFrom(SampleModule);

        literals.ShouldContain((LiteralOwnerName, "read by the owner"));
        literals.ShouldContain(($"{LiteralOwnerName}+NestedReader", "read by the nested reader"));
        literals.ShouldContain(($"{LiteralOwnerName}+Row", "Update = "));
        literals.ShouldNotContain((LiteralOwnerName, "Update = "));
    }

    [Fact]
    public void Sql_of_a_type_nested_in_a_query_handler_is_checked()
    {
        var literals = ReadQueryHandlerLiterals(SampleModule);

        literals.ShouldContain(($"{SampleQueryHandlerName}+Sql", "UPDATE samples.rows SET label = 'renamed'"));
    }

    [Theory]
    [InlineData("INSERT INTO pongs.pongs (id) VALUES (@Id)")]
    [InlineData("UPDATE pongs.pongs SET label = 'x'")]
    [InlineData("update \"pongs\".\"pongs\" AS pong set label = @Label")]
    [InlineData("DELETE FROM pongs.pongs WHERE id = @Id")]
    [InlineData("MERGE INTO pongs.pongs USING pongs.incoming ON true WHEN MATCHED THEN DO NOTHING")]
    [InlineData("TRUNCATE pongs.pongs")]
    [InlineData("truncate table only \"pongs\".\"pongs\"")]
    public void Sql_that_changes_rows_is_a_writing_statement(String sql) =>
        WritingStatement
            .IsMatch(sql)
            .ShouldBeTrue();

    [Theory]
    [InlineData("Nothing to update")]
    [InlineData("cannot delete a ping")]
    [InlineData("Update = ")]
    [InlineData("Truncate = ")]
    [InlineData("SELECT updated_on, deleted FROM pongs.pongs")]
    public void Prose_and_reads_are_not_writing_statements(String text) =>
        WritingStatement
            .IsMatch(text)
            .ShouldBeFalse();

    [Theory]
    [InlineData("SELECT * FROM pings.pings")]
    [InlineData("SELECT * FROM pings.pings--comment")]
    [InlineData("SELECT * FROM \"pings\".\"pings\"")]
    [InlineData("SELECT label FROM PINGS.pings")]
    public void Sql_that_reads_a_schema_names_it(String sql) =>
        NamesSchema(sql, "pings")
            .ShouldBeTrue();

    [Theory]
    [InlineData("SELECT own.pings.id FROM own.pings")]
    [InlineData("Tsikhanau.Monolith.TestModules.Pings.Features.GetPing")]
    [InlineData("pings.ping-created")]
    [InlineData("too many pings.")]
    public void Text_that_only_mentions_a_schema_does_not_name_it(String text) =>
        NamesSchema(text, "pings")
            .ShouldBeFalse();

    [Theory]
    [InlineData("SELECT * FROM pings.pings")]
    [InlineData("SELECT ping.* FROM pings.pings AS ping")]
    [InlineData("SELECT \"ping\".* FROM pings.pings AS ping")]
    [InlineData("SELECT id, ping.* FROM pings.pings AS ping")]
    [InlineData("SELECT DISTINCT * FROM pings.pings")]
    [InlineData("select * from pings.pings")]
    [InlineData("INSERT INTO pings.pings (id) VALUES (@Id) RETURNING *")]
    [InlineData("UPDATE pings.pings SET label = @Label RETURNING ping.*")]
    public void Sql_that_reads_every_column_is_a_star_column(String sql) =>
        StarColumn
            .IsMatch(sql)
            .ShouldBeTrue();

    [Theory]
    [InlineData("SELECT count(*)::int FROM pings.pings")]
    [InlineData("SELECT COUNT(*) FROM pings.pings")]
    [InlineData("SELECT id, count(*) FROM pings.pings GROUP BY id")]
    [InlineData("SELECT EXISTS (SELECT 1 FROM pings.pings)")]
    [InlineData("SELECT id, label FROM pings.pings")]
    [InlineData("SELECT price * 2 FROM pings.pings")]
    [InlineData("RETURNING id")]
    [InlineData("Select the star * to continue")]
    [InlineData("Returning visitor")]
    public void Sql_that_names_its_columns_is_not_a_star_column(String text) =>
        StarColumn
            .IsMatch(text)
            .ShouldBeFalse();

    private static List<(String TypeName, String Literal)> ReadQueryHandlerLiterals(ArchModule module)
    {
        var queryHandlerNames = module.Types
            .Where(type => type.IsConcrete && type.Implements(typeof(IQueryHandler<,>)))
            .Select(type => type.FullName)
            .OfType<String>()
            .ToList();

        return StringLiterals
            .ReadFrom(module)
            .Where(literal => queryHandlerNames.Any(handlerName => IsOrIsNestedIn(literal.TypeName, handlerName)))
            .ToList();
    }

    private static Boolean IsOrIsNestedIn(String typeName, String handlerName) =>
        typeName == handlerName || typeName.StartsWith($"{handlerName}+", StringComparison.Ordinal);

    private static String ReadSchema(String moduleName)
    {
        var dbContextType = ArchModules
            .Get(moduleName).Types
            .Single(type => type.IsConcrete && type.Implements<ModuleDbContext>());

        var schemaField = dbContextType.GetField(SchemaNameField, ConstantFields);
        return schemaField?.GetRawConstantValue() as String
            ?? throw new InvalidOperationException($"{dbContextType.FullName} has no const String {SchemaNameField}");
    }

    private static Boolean NamesSchema(String sql, String schema) =>
        Regex.IsMatch(
            sql,
            $$"""(?<![\w."])"?{{Regex.Escape(schema)}}"?\."?[a-z_][a-z0-9_]*\b(?!-\w)""",
            RegexOptions.IgnoreCase);

    [GeneratedRegex(WritingStatementPattern, RegexOptions.IgnoreCase)]
    private static partial Regex WritingStatement { get; }

    [GeneratedRegex(StarColumnPattern, RegexOptions.IgnoreCase)]
    private static partial Regex StarColumn { get; }

    private sealed class LiteralOwner
    {
        public static async Task<String> ReadAsync()
        {
            await Task.Yield();
            return "read by the owner";
        }

        public sealed record Row(String Update);

        public sealed class NestedReader
        {
            public static async Task<String> ReadAsync()
            {
                await Task.Yield();
                return "read by the nested reader";
            }
        }
    }

    private sealed record SampleQuery : IQuery<String>;

    private sealed class SampleQueryHandler : IQueryHandler<SampleQuery, String>
    {
        public Task<Result<String>> Handle(SampleQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<Result<String>>(Sql.Rename);

        private static class Sql
        {
            public static String Rename => "UPDATE samples.rows SET label = 'renamed'";
        }
    }
}
