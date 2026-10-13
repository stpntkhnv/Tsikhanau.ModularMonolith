using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Tsikhanau.Monolith.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SqlConstantAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = RuleLines.Explain(
        new(
            "TS0006",
            "SQL passed to Dapper or EF Core raw SQL must be a constant",
            "SQL passed to Dapper or EF Core raw SQL must be a constant: put values in parameters (@Name), "
            + "never into the text",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true));

    private static readonly ImmutableHashSet<String> RawDatabaseSqlMethods =
        ImmutableHashSet.Create("ExecuteSqlRaw", "ExecuteSqlRawAsync", "SqlQueryRaw");

    private static readonly (String TypeName, String ParameterName, Func<IMethodSymbol, Boolean> TakesSql)[] SqlSinks =
    [
        ("Dapper.SqlMapper", "sql", AnyMethod),
        ("Dapper.CommandDefinition", "commandText", IsConstructor),
        ("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "sql", IsRawDatabaseSqlMethod),
        ("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions", "sql", IsRawQuerySqlMethod),
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(RegisterSqlArgumentAction);
    }

    private static void RegisterSqlArgumentAction(CompilationStartAnalysisContext context)
    {
        var sqlParameters = GetSqlParameters(context.Compilation);
        if (sqlParameters.IsEmpty)
        {
            return;
        }

        context.RegisterOperationAction(
            operationContext => AnalyzeArguments(operationContext, sqlParameters),
            OperationKind.Invocation,
            OperationKind.ObjectCreation);
    }

    private static ImmutableHashSet<IParameterSymbol> GetSqlParameters(Compilation compilation) =>
        SqlSinks
            .SelectMany(sink => GetSqlParameters(compilation, sink.TypeName, sink.ParameterName, sink.TakesSql))
            .ToImmutableHashSet<IParameterSymbol>(SymbolEqualityComparer.Default);

    private static IEnumerable<IParameterSymbol> GetSqlParameters(
        Compilation compilation,
        String typeName,
        String parameterName,
        Func<IMethodSymbol, Boolean> takesSql) =>
        compilation.GetTypeByMetadataName(typeName) is { } sinkType
            ? sinkType
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(takesSql)
                .SelectMany(method => method.Parameters)
                .Where(parameter => parameter.Name == parameterName)
            : [];

    private static void AnalyzeArguments(
        OperationAnalysisContext context,
        ImmutableHashSet<IParameterSymbol> sqlParameters)
    {
        foreach (var argument in GetArguments(context.Operation))
        {
            if (IsSqlArgument(argument, sqlParameters) && !IsConstant(argument.Value))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, argument.Value.Syntax.GetLocation()));
            }
        }
    }

    private static ImmutableArray<IArgumentOperation> GetArguments(IOperation operation) =>
        operation switch
        {
            IInvocationOperation invocation => invocation.Arguments,
            IObjectCreationOperation objectCreation => objectCreation.Arguments,
            _ => ImmutableArray<IArgumentOperation>.Empty,
        };

    private static Boolean IsSqlArgument(
        IArgumentOperation argument,
        ImmutableHashSet<IParameterSymbol> sqlParameters) =>
        argument.Parameter is { } parameter && sqlParameters.Contains(parameter.OriginalDefinition);

    private static Boolean IsConstant(IOperation value) =>
        value is IConversionOperation { IsImplicit: true } conversion
            ? IsConstant(conversion.Operand)
            : value.ConstantValue.HasValue;

    private static Boolean AnyMethod(IMethodSymbol method) => true;

    private static Boolean IsConstructor(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Constructor;

    private static Boolean IsRawDatabaseSqlMethod(IMethodSymbol method) =>
        RawDatabaseSqlMethods.Contains(method.Name);

    private static Boolean IsRawQuerySqlMethod(IMethodSymbol method) =>
        method.Name == "FromSqlRaw";
}
