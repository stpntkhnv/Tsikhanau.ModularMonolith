using Microsoft.CodeAnalysis;

namespace Tsikhanau.Railway.Analyzers;

internal sealed class RailwayTypes
{
    private readonly INamedTypeSymbol _result;
    private readonly INamedTypeSymbol? _resultExtensions;
    private readonly INamedTypeSymbol? _task;
    private readonly INamedTypeSymbol? _valueTask;
    private readonly INamedTypeSymbol _nullable;

    private RailwayTypes(Compilation compilation, INamedTypeSymbol result)
    {
        _result = result;
        _resultExtensions = compilation.GetTypeByMetadataName("Tsikhanau.Railway.ResultExtensions");
        _task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        _valueTask = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
        _nullable = compilation.GetSpecialType(SpecialType.System_Nullable_T);
    }

    public static RailwayTypes? Create(Compilation compilation) =>
        compilation.GetTypeByMetadataName("Tsikhanau.Railway.Result`1") is { } result
            ? new RailwayTypes(compilation, result)
            : null;

    public Boolean IsResult(ITypeSymbol? type) =>
        type is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, _result);

    public Boolean IsNestedResult(ITypeSymbol? type) =>
        type is INamedTypeSymbol named && IsResult(named) && IsResult(named.TypeArguments[0]);

    public ITypeSymbol? GetAwaitedType(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeArguments.Length: 1 } named && (IsDefinition(named, _task) || IsDefinition(named, _valueTask))
            ? named.TypeArguments[0]
            : null;

    public Boolean IsResultOrAwaitableResult(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol { TypeArguments.Length: 1 } named && IsDefinition(named, _nullable))
        {
            type = named.TypeArguments[0];
        }

        return IsResult(type) || IsResult(GetAwaitedType(type));
    }

    public Boolean IsResultExtension(IMethodSymbol method) =>
        SymbolEqualityComparer.Default.Equals(method.ContainingType, _resultExtensions);

    public ResultMember GetMember(IPropertySymbol property)
    {
        if (!IsResult(property.ContainingType))
        {
            return ResultMember.None;
        }

        return property.Name switch
        {
            "IsSuccess" => ResultMember.IsSuccess,
            "IsFailure" => ResultMember.IsFailure,
            "Value" => ResultMember.Value,
            "Error" => ResultMember.Error,
            _ => ResultMember.None
        };
    }

    private static Boolean IsDefinition(INamedTypeSymbol type, INamedTypeSymbol? definition) =>
        SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, definition);
}
