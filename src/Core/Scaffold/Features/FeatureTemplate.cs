using Tsikhanau.Monolith.Scaffold.Transactions;
using static Tsikhanau.Monolith.Scaffold.Modules.CodeLayout;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed class FeatureTemplate(ScaffoldedFeature feature)
{
    private const String RailwayNamespace = "Tsikhanau.Railway";

    private const String Unit = "Unit";

    private const String NotImplementedDeclaration = "    private static readonly Error NotImplemented =";

    private const String NotImplementedFailure = "Task.FromResult(Result.Failure(NotImplemented))";

    private static readonly IReadOnlyList<String> EndpointResults =
        ["NoContent", "ValidationProblem", "ProblemHttpResult"];

    public IReadOnlyList<NewFile> CreateFiles() => [CommandFile, CommandHandlerFile, .. EndpointFiles];

    private IEnumerable<NewFile> EndpointFiles =>
        feature.Method switch
        {
            EndpointMethod.Post or EndpointMethod.Put => [RequestFile, EndpointFile],
            EndpointMethod.Delete => [EndpointFile],
            _ => [],
        };

    private NewFile CommandFile =>
        new(
            feature.InFeature($"{feature.Command}.cs"),
            CodeFile(
                [RailwayNamespace],
                RecordDeclaration(feature.Command, $"{feature.Module.CommandMarker}<{Unit}>")));

    private NewFile CommandHandlerFile =>
        new(
            feature.InFeature($"{feature.CommandHandler}.cs"),
            CodeFile(
                ["Tsikhanau.Monolith.Application.Commands", RailwayNamespace],
                $$"""
                {{HandlerDeclaration(feature.CommandHandler, "ICommandHandler", [feature.Command, Unit])}}
                {
                {{NotImplementedField}}

                {{HandleMethod(feature.Command, "command", Unit)}}
                }
                """));

    private NewFile RequestFile =>
        new(feature.InFeature($"{feature.RequestRecord}.cs"), CodeFile([], RecordDeclaration(feature.RequestRecord)));

    private NewFile EndpointFile =>
        new(
            feature.InFeature($"{feature.Endpoint}.cs"),
            CodeFile(
                [
                    "Microsoft.AspNetCore.Builder",
                    "Microsoft.AspNetCore.Http.HttpResults",
                    "Microsoft.AspNetCore.Routing",
                    "Tsikhanau.Monolith.Api.Endpoints",
                ],
                $$"""
                internal sealed class {{feature.Endpoint}} : IEndpoint
                {
                    public void Map(IEndpointRouteBuilder endpoints) =>
                        endpoints
                {{MapCall}}
                            .WithName("{{feature.RouteName}}");

                {{EndpointSignature}}
                        {{feature.FacadeParameter}}
                {{FacadeCall}}
                            .ToNoContentAsync();
                }
                """));

    private String NotImplementedField =>
        FirstThatFits(
            $"{NotImplementedDeclaration} {NotImplementedError};",
            $"""
             {NotImplementedDeclaration}
                     {NotImplementedError};
             """,
            $"""
             {NotImplementedDeclaration}
                     Error.Failure(
                         "{feature.NotImplementedCode}",
                         "{feature.NotImplementedMessage}");
             """);

    private String NotImplementedError =>
        $"Error.Failure(\"{feature.NotImplementedCode}\", \"{feature.NotImplementedMessage}\")";

    private String MapCall =>
        FirstThatFits(
            $"""            .Map{feature.Method}("{feature.Route}", {EndpointHandler})""",
            $"""
                         .Map{feature.Method}(
                             "{feature.Route}",
                             {EndpointHandler})
             """);

    private String EndpointHandler => $"{feature.Name}Async";

    private String EndpointSignature =>
        FirstThatFits(
            $"""
                 private static Task<Results<{String.Join(", ", EndpointResults)}>> {EndpointHandler}(
                     {EndpointParameters}) =>
             """,
            $"""
                 private static Task<Results<
                     {String.Join(",\n        ", EndpointResults)}>> {EndpointHandler}(
                     {EndpointParameters}) =>
             """);

    private String EndpointParameters =>
        String.Join(
            ",\n        ",
            [
                .. RequestParameter,
                $"{feature.Module.FacadeContract} {feature.FacadeParameter}",
                "CancellationToken cancellationToken",
            ]);

    private IEnumerable<String> RequestParameter => feature.HasRequestBody ? [$"{feature.RequestRecord} request"] : [];

    private String FacadeCall =>
        FirstThatFits(
            $"""            .ExecuteCommandAsync(new {feature.Command}(), cancellationToken)""",
            $"""
                         .ExecuteCommandAsync(
                             new {feature.Command}(),
                             cancellationToken)
             """);

    private static String HandlerDeclaration(
        String handler,
        String handlerInterface,
        IReadOnlyList<String> typeArguments)
    {
        var baseType = $"{handlerInterface}<{String.Join(", ", typeArguments)}>";

        return FirstThatFits(
            $"internal sealed class {handler} : {baseType}",
            $"""
             internal sealed class {handler}
                 : {baseType}
             """,
            $"""
             internal sealed class {handler}
                 : {handlerInterface}<
                     {String.Join(",\n        ", typeArguments)}>
             """);
    }

    private static String HandleMethod(String request, String requestParameter, String result)
    {
        var head = $"public Task<Result<{result}>> Handle";
        var parameter = $"{request} {requestParameter}";

        return FirstThatFits(
            $"""
                 {head}({parameter}, CancellationToken cancellationToken) =>
                     {NotImplementedFailure};
             """,
            $"""
                 {head}(
                     {parameter},
                     CancellationToken cancellationToken) =>
                     {NotImplementedFailure};
             """);
    }

    private static String RecordDeclaration(String name) => $"internal sealed record {name};";

    private static String RecordDeclaration(String name, String baseType) =>
        FirstThatFits(
            $"internal sealed record {name} : {baseType};",
            $"""
             internal sealed record {name}
                 : {baseType};
             """);

    private String CodeFile(IReadOnlyList<String> usings, String body) =>
        $"{UsingBlock(usings)}namespace {feature.Namespace};\n\n{body}\n";

    private static String UsingBlock(IReadOnlyList<String> usings) =>
        usings.Count == 0 ? String.Empty : $"{Usings(usings)}\n\n";
}
