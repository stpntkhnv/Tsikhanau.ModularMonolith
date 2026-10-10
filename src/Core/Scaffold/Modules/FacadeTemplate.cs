using Tsikhanau.Monolith.Scaffold.Transactions;
using static Tsikhanau.Monolith.Scaffold.Modules.CodeLayout;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed class FacadeTemplate(ScaffoldedModule module)
{
    private const String RailwayNamespace = "Tsikhanau.Railway";

    private const String ExecuteCommandAsync = "Task<Result<TResult>> ExecuteCommandAsync<TResult>";

    private const String ExecuteQueryAsync = "Task<Result<TResult>> ExecuteQueryAsync<TResult>";

    private const String SendCommand = " =>\n        SendAsync(command, cancellationToken);";

    private const String SendQuery = " =>\n        QueryAsync(query, cancellationToken);";

    public String FacadeContractPath => module.InProject($"{module.FacadeContract}.cs");

    public String FacadeClassPath => module.InProject($"Infrastructure/{module.FacadeClass}.cs");

    public String CommandMarkerPath => module.InProject($"{module.CommandMarker}.cs");

    public String QueryMarkerPath => module.InContracts($"{module.QueryMarker}.cs");

    public String QueryFacadePath => module.InContracts($"{module.QueryFacade}.cs");

    public NewFile FacadeContractFile => new(FacadeContractPath, RenderFacadeContract(FacadeShape.Fresh));

    public NewFile FacadeClassFile => new(FacadeClassPath, RenderFacadeClass(FacadeShape.Fresh));

    public NewFile CommandMarkerFile =>
        new(
            CommandMarkerPath,
            $$"""
            {{Usings("Tsikhanau.Monolith.Application.Commands")}}

            namespace {{module.Namespace}};

            internal interface {{module.CommandMarker}}<TResult> : ICommand<TResult>
                where TResult : notnull;

            """);

    public NewFile QueryMarkerFile =>
        new(
            QueryMarkerPath,
            $$"""
            {{Usings("Tsikhanau.Monolith.Contracts.Queries")}}

            namespace {{module.ContractsNamespace}};

            public interface {{module.QueryMarker}}<TResult> : IQuery<TResult>
                where TResult : notnull;

            """);

    public NewFile QueryFacadeFile =>
        new(
            QueryFacadePath,
            $$"""
            {{Usings("Tsikhanau.Monolith.Contracts.Modules", RailwayNamespace)}}

            namespace {{module.ContractsNamespace}};

            public interface {{module.QueryFacade}} : IModuleQueryFacade
            {
            {{ExecuteQueryDeclaration}}
            }

            """);

    private String CommandParameter => $"{module.CommandMarker}<TResult> command";

    private String QueryParameter => $"{module.QueryMarker}<TResult> query";

    private String ExecuteCommandDeclaration => GenericMethod(ExecuteCommandAsync, CommandParameter, ";");

    private String ExecuteQueryDeclaration => GenericMethod(ExecuteQueryAsync, QueryParameter, ";");

    private String ExecuteCommandImplementation =>
        GenericMethod($"public {ExecuteCommandAsync}", CommandParameter, SendCommand);

    private String ExecuteQueryImplementation =>
        GenericMethod($"public {ExecuteQueryAsync}", QueryParameter, SendQuery);

    public String RenderFacadeContract(FacadeShape shape) =>
        $$"""
        {{Usings(FacadeContractUsings(shape))}}

        namespace {{module.Namespace}};

        {{Declare(end => FacadeContractHead(shape, end), FacadeContractMembers(shape))}}

        """;

    public String RenderFacadeClass(FacadeShape shape) =>
        $$"""
        {{Usings(FacadeClassUsings(shape))}}

        namespace {{module.Namespace}}.Infrastructure;

        {{Declare(FacadeClassHead, FacadeClassMembers(shape))}}

        """;

    private IEnumerable<String> FacadeContractUsings(FacadeShape shape) =>
    [
        "Tsikhanau.Monolith.Application.Modules",
        .. IncludeIf(shape.HasQueries, module.ContractsNamespace),
        .. IncludeIf(shape.HasCommands, RailwayNamespace),
    ];

    private IEnumerable<String> FacadeClassUsings(FacadeShape shape) =>
    [
        "Microsoft.Extensions.DependencyInjection",
        "Tsikhanau.Monolith.Infrastructure.Dispatching",
        .. IncludeIf(shape.HasQueries, module.ContractsNamespace),
        .. IncludeIf(shape.HasCommands || shape.HasQueries, RailwayNamespace),
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
