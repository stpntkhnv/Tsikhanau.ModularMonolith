using System.Reflection;
using System.Text.RegularExpressions;
using ModularMonolith.ArchTests.Support;
using ModularMonolith.BuildingBlocks.Application.Queries;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.Shared.Reflection;

namespace ModularMonolith.ArchTests.Modules;

public sealed partial class SqlTextTests
{
    private const String SchemaNameField = "SchemaName";
    private const BindingFlags ConstantFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [EveryModule]
    public void Query_handlers_only_read(String name)
    {
        var module = ArchModules.Get(name);
        var queryHandlerNames = module.Types
            .Where(type => type.IsConcrete && type.Implements(typeof(IQueryHandler<,>)))
            .Select(type => type.FullName)
            .ToHashSet();

        var queryHandlerLiterals = StringLiterals
            .ReadFrom(module)
            .Where(literal => queryHandlerNames.Contains(literal.TypeName))
            .ToList();

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

    [Fact]
    public void Literals_inside_async_state_machines_are_read()
    {
        var literals = StringLiterals.ReadFrom(ArchModules.Get("Pongs"));

        literals.ShouldContain((
            "ModularMonolith.TestModules.Pongs.Features.CountPongs.CountPongsQueryHandler",
            "SELECT count(*)::int FROM pongs.pongs"));
    }

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
            $"\\b\"?{Regex.Escape(schema)}\"?\\.\"?[a-z_][a-z0-9_]*\\b(?!-)",
            RegexOptions.IgnoreCase);

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WritingStatement { get; }
}
