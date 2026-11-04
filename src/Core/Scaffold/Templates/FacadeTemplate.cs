using Tsikhanau.Monolith.Scaffold.Facades;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Names;
using Tsikhanau.Monolith.Scaffold.Plans;
using static Tsikhanau.Monolith.Scaffold.Templates.CodeLayout;

namespace Tsikhanau.Monolith.Scaffold.Templates;

internal sealed class FacadeTemplate(ScaffoldedModule module)
{
    private const String ExecuteCommandHead = $"Task<Result<TResult>> {EmittedNames.ExecuteCommandAsync}<TResult>";

    private const String ExecuteQueryHead = $"Task<Result<TResult>> {EmittedNames.ExecuteQueryAsync}<TResult>";

    private const String SendCommand = " =>\n        SendAsync(command, cancellationToken);";

    private const String SendQuery = " =>\n        QueryAsync(query, cancellationToken);";

    public String FacadeContractPath => module.Projects.InProject($"{module.FacadeContract}.cs");

    public String FacadeClassPath => module.Projects.InProject($"Infrastructure/{module.FacadeClass}.cs");

    public String CommandMarkerPath => module.Projects.InProject($"{module.CommandMarker}.cs");

    public String QueryMarkerPath => module.Projects.InContracts($"{module.QueryMarker}.cs");

    public String QueryFacadePath => module.Projects.InContracts($"{module.QueryFacade}.cs");

    public NewFile FacadeContractFile => new(FacadeContractPath, RenderFacadeContract(FacadeShape.Fresh));

    public NewFile FacadeClassFile => new(FacadeClassPath, RenderFacadeClass(FacadeShape.Fresh));

    public NewFile CommandMarkerFile =>
        new(
            CommandMarkerPath,
            CodeFile(
                module.Namespace,
                ["Tsikhanau.Monolith.Application.Commands"],
                $"""
                 internal interface {module.CommandMarker}<TResult> : ICommand<TResult>
                     where TResult : notnull;
                 """));

    public NewFile QueryMarkerFile =>
        new(
            QueryMarkerPath,
            CodeFile(
                module.ContractsNamespace,
                ["Tsikhanau.Monolith.Contracts.Queries"],
                $"""
                 public interface {module.QueryMarker}<TResult> : IQuery<TResult>
                     where TResult : notnull;
                 """));

    public NewFile QueryFacadeFile =>
        new(
            QueryFacadePath,
            CodeFile(
                module.ContractsNamespace,
                ["Tsikhanau.Monolith.Contracts.Modules", EmittedNames.RailwayNamespace],
                $$"""
                public interface {{module.QueryFacade}} : IModuleQueryFacade
                {
                {{ExecuteQueryDeclaration}}
                }
                """));

    private String CommandParameter => $"{module.CommandMarker}<TResult> command";

    private String QueryParameter => $"{module.QueryMarker}<TResult> query";

    private String ExecuteCommandDeclaration => GenericMethod(ExecuteCommandHead, CommandParameter, ";");

    private String ExecuteQueryDeclaration => GenericMethod(ExecuteQueryHead, QueryParameter, ";");

    private String ExecuteCommandImplementation =>
        GenericMethod($"public {ExecuteCommandHead}", CommandParameter, SendCommand);

    private String ExecuteQueryImplementation =>
        GenericMethod($"public {ExecuteQueryHead}", QueryParameter, SendQuery);

    public String RenderFacadeContract(FacadeShape shape) =>
        CodeFile(
            module.Namespace,
            FacadeContractUsings(shape),
            Declare(end => FacadeContractHead(shape, end), FacadeContractMembers(shape)));

    public String RenderFacadeClass(FacadeShape shape) =>
        CodeFile(
            module.InfrastructureNamespace,
            FacadeClassUsings(shape),
            Declare(FacadeClassHead, FacadeClassMembers(shape)));

    private IReadOnlyList<String> FacadeContractUsings(FacadeShape shape) =>
    [
        "Tsikhanau.Monolith.Application.Modules",
        .. IncludeIf(shape.HasQueries, module.ContractsNamespace),
        .. IncludeIf(shape.HasCommands, EmittedNames.RailwayNamespace),
    ];

    private IReadOnlyList<String> FacadeClassUsings(FacadeShape shape) =>
    [
        "Microsoft.Extensions.DependencyInjection",
        "Tsikhanau.Monolith.Infrastructure.Dispatching",
        .. IncludeIf(shape.HasQueries, module.ContractsNamespace),
        .. IncludeIf(shape.HasCommands || shape.HasQueries, EmittedNames.RailwayNamespace),
    ];

    private IReadOnlyList<String> FacadeContractMembers(FacadeShape shape) =>
        [.. IncludeIf(shape.HasCommands, ExecuteCommandDeclaration)];

    private IReadOnlyList<String> FacadeClassMembers(FacadeShape shape) =>
    [
        .. IncludeIf(shape.HasCommands, ExecuteCommandImplementation),
        .. IncludeIf(shape.HasQueries, ExecuteQueryImplementation),
    ];

    private String FacadeContractHead(FacadeShape shape, String end)
    {
        var baseTypes = shape.HasQueries ? $"{module.QueryFacade}, IModuleFacade" : "IModuleFacade";

        return FirstThatFits(
            $"internal interface {module.FacadeContract} : {baseTypes}{end}",
            $"""
             internal interface {module.FacadeContract}
                 : {baseTypes}{end}
             """);
    }

    private String FacadeClassHead(String end) =>
        FirstThatFits(
            $"""
             internal sealed class {module.FacadeClass}(IServiceScopeFactory serviceScopeFactory)
                 : ModuleFacade<{module.FacadeContract}>(serviceScopeFactory), {module.FacadeContract}{end}
             """,
            $"""
             internal sealed class {module.FacadeClass}(IServiceScopeFactory serviceScopeFactory)
                 : ModuleFacade<{module.FacadeContract}>(serviceScopeFactory),
                     {module.FacadeContract}{end}
             """,
            $"""
             internal sealed class {module.FacadeClass}(
                 IServiceScopeFactory serviceScopeFactory)
                 : ModuleFacade<{module.FacadeContract}>(serviceScopeFactory),
                     {module.FacadeContract}{end}
             """);

    private static String Declare(Func<String, String> head, IReadOnlyList<String> members) =>
        members.Count == 0 ? head(";") : $"{head(String.Empty)}\n{{\n{String.Join("\n\n", members)}\n}}";

    private static IEnumerable<String> IncludeIf(Boolean condition, String text) => condition ? [text] : [];

    private static String GenericMethod(String head, String firstParameter, String tail) =>
        FirstThatFits(
            $"""
                 {head}({firstParameter}, CancellationToken cancellationToken)
                     where TResult : notnull{tail}
             """,
            $"""
                 {head}(
                     {firstParameter},
                     CancellationToken cancellationToken)
                     where TResult : notnull{tail}
             """);
}
