using Tsikhanau.Monolith.Scaffold.Transactions;
using static Tsikhanau.Monolith.Scaffold.Modules.CodeLayout;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed class FacadeTemplate(ScaffoldedModule module)
{
    private const String RailwayNamespace = "Tsikhanau.Railway";

    private const String ExecuteCommandAsync = "Task<Result<TResult>> ExecuteCommandAsync<TResult>";

    private const String SendCommand = " =>\n        SendAsync(command, cancellationToken);";

    public String FacadeContractPath => module.InProject($"{module.FacadeContract}.cs");

    public String FacadeClassPath => module.InProject($"Infrastructure/{module.FacadeClass}.cs");

    public String CommandMarkerPath => module.InProject($"{module.CommandMarker}.cs");

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

    private String CommandParameter => $"{module.CommandMarker}<TResult> command";

    private String ExecuteCommandDeclaration => GenericMethod(ExecuteCommandAsync, CommandParameter, ";");

    private String ExecuteCommandImplementation =>
        GenericMethod($"public {ExecuteCommandAsync}", CommandParameter, SendCommand);

    public String RenderFacadeContract(FacadeShape shape) =>
        $$"""
        {{Usings(FacadeContractUsings(shape))}}

        namespace {{module.Namespace}};

        {{FacadeContractDeclaration(shape)}}

        """;

    public String RenderFacadeClass(FacadeShape shape) =>
        $$"""
        {{Usings(FacadeClassUsings(shape))}}

        namespace {{module.Namespace}}.Infrastructure;

        {{FacadeClassDeclaration(shape)}}

        """;

    private static IEnumerable<String> FacadeContractUsings(FacadeShape shape) =>
        ["Tsikhanau.Monolith.Application.Modules", .. CommandUsings(shape)];

    private static IEnumerable<String> FacadeClassUsings(FacadeShape shape) =>
    [
        "Microsoft.Extensions.DependencyInjection",
        "Tsikhanau.Monolith.Infrastructure.Dispatching",
        .. CommandUsings(shape),
    ];

    private static IEnumerable<String> CommandUsings(FacadeShape shape) => shape.HasCommands ? [RailwayNamespace] : [];

    private String FacadeContractDeclaration(FacadeShape shape) =>
        shape.HasCommands
            ? WithBody(FacadeContractHead(String.Empty), ExecuteCommandDeclaration)
            : FacadeContractHead(";");

    private String FacadeContractHead(String end) =>
        FirstThatFits(
            $"internal interface {module.FacadeContract} : IModuleFacade{end}",
            $"""
             internal interface {module.FacadeContract}
                 : IModuleFacade{end}
             """);

    private String FacadeClassDeclaration(FacadeShape shape) =>
        shape.HasCommands
            ? WithBody(FacadeClassHead(String.Empty), ExecuteCommandImplementation)
            : FacadeClassHead(";");

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

    private static String WithBody(String head, String member) => $"{head}\n{{\n{member}\n}}";

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
