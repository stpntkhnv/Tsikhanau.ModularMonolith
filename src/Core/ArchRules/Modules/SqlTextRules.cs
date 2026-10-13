using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class SqlTextRules
{
    public static RuleEvaluation QueryHandlersOnlyRead(ArchModule module)
    {
        var literals = ReadQueryHandlerLiterals(module);

        return RuleEvaluation.Create(
            Describe(literals),
            () => literals
                .Where(literal => SqlPatterns.WritingStatement.IsMatch(literal.Literal))
                .Select(literal => $"{literal.TypeName} writes: {literal.Literal}"));
    }

    public static RuleEvaluation ModuleSqlNamesNoOtherModuleSchema(ArchModules modules, ArchModule module)
    {
        var literals = StringLiterals.ReadFrom(module);

        return RuleEvaluation.Create(
            Describe(literals),
            () => GetLiteralsNamingOtherSchemas(modules, module, literals));
    }

    public static RuleEvaluation ModuleSqlNamesItsColumns(ArchModule module)
    {
        var literals = StringLiterals.ReadFrom(module);

        return RuleEvaluation.Create(
            Describe(literals),
            () => literals
                .Where(literal => SqlScanner.ReadsEveryColumn(literal.Literal))
                .Select(literal => $"{literal.TypeName} reads every column: {literal.Literal}"));
    }

    private static IEnumerable<String> GetLiteralsNamingOtherSchemas(
        ArchModules modules,
        ArchModule module,
        IReadOnlyList<(String TypeName, String Literal)> literals)
    {
        var otherSchemas = modules
            .GetOthers(module)
            .SelectMany(ReadSchemas)
            .ToList();

        return literals
            .Where(literal => otherSchemas.Any(schema => SqlPatterns.NamesSchema(literal.Literal, schema)))
            .Select(literal => $"{literal.TypeName} reaches another module: {literal.Literal}");
    }

    private static IReadOnlyList<String> Describe(IReadOnlyList<(String TypeName, String Literal)> literals) =>
        [.. literals.Select(literal => $"{literal.TypeName}: {literal.Literal}")];

    private static List<(String TypeName, String Literal)> ReadQueryHandlerLiterals(ArchModule module)
    {
        var queryHandlerNames = module.Types
            .Where(type => type.IsConcrete && type.Implements(typeof(IQueryHandler<,>)))
            .Select(type => type.FullNameOrName)
            .ToList();

        return StringLiterals
            .ReadFrom(module)
            .Where(literal => queryHandlerNames.Any(handlerName => IsOrIsNestedIn(literal.TypeName, handlerName)))
            .ToList();
    }

    private static Boolean IsOrIsNestedIn(String typeName, String handlerName) =>
        typeName == handlerName || typeName.StartsWith($"{handlerName}+", StringComparison.Ordinal);

    private static IEnumerable<String> ReadSchemas(ArchModule module) =>
        module.Types
            .Where(type => type.IsConcrete && type.Implements<ModuleDbContext>())
            .Select(ModuleSchema.Get);
}
