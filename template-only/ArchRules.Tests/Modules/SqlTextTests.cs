using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.ArchRules.Tests.Inputs;
using Tsikhanau.Monolith.Contracts.Queries;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.ArchRules.Tests.Modules;

public sealed class SqlTextTests : SqlTextTests<TestModuleInputs>
{
    private const String SampleNamespace = "Tsikhanau.Monolith.ArchRules.Tests.Modules";
    private const String LiteralOwnerName = $"{SampleNamespace}.SqlTextTests+LiteralOwner";
    private const String SampleQueryHandlerName = $"{SampleNamespace}.SqlTextTests+SampleQueryHandler";

    private static ArchModule SampleModule => new("Samples", SampleNamespace, [typeof(SqlTextTests).Assembly]);

    [Fact]
    public void Literals_inside_async_state_machines_are_read()
    {
        var literals = StringLiterals.ReadFrom(GetModule("Pongs"));

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
        var evaluation = SqlTextRules.QueryHandlersOnlyRead(SampleModule);

        evaluation.Violations.ShouldContain(
            $"{SampleQueryHandlerName}+Sql writes: UPDATE samples.rows SET label = 'renamed'");
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
        SqlPatterns.WritingStatement
            .IsMatch(sql)
            .ShouldBeTrue();

    [Theory]
    [InlineData("Nothing to update")]
    [InlineData("cannot delete a ping")]
    [InlineData("Update = ")]
    [InlineData("Truncate = ")]
    [InlineData("SELECT updated_on, deleted FROM pongs.pongs")]
    public void Prose_and_reads_are_not_writing_statements(String text) =>
        SqlPatterns.WritingStatement
            .IsMatch(text)
            .ShouldBeFalse();

    [Theory]
    [InlineData("SELECT * FROM pings.pings")]
    [InlineData("SELECT * FROM pings.pings--comment")]
    [InlineData("SELECT * FROM \"pings\".\"pings\"")]
    [InlineData("SELECT label FROM PINGS.pings")]
    public void Sql_that_reads_a_schema_names_it(String sql) =>
        SqlPatterns
            .NamesSchema(sql, "pings")
            .ShouldBeTrue();

    [Theory]
    [InlineData("SELECT own.pings.id FROM own.pings")]
    [InlineData("Tsikhanau.Monolith.TestModules.Pings.Features.GetPing")]
    [InlineData("pings.ping-created")]
    [InlineData("too many pings.")]
    public void Text_that_only_mentions_a_schema_does_not_name_it(String text) =>
        SqlPatterns
            .NamesSchema(text, "pings")
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
        SqlPatterns.StarColumn
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
        SqlPatterns.StarColumn
            .IsMatch(text)
            .ShouldBeFalse();

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
