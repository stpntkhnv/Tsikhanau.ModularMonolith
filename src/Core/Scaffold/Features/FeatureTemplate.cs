using Tsikhanau.Monolith.Scaffold.Events;
using Tsikhanau.Monolith.Scaffold.Transactions;
using static Tsikhanau.Monolith.Scaffold.Modules.CodeLayout;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed class FeatureTemplate(ScaffoldedFeature feature)
{
    private const String RailwayNamespace = "Tsikhanau.Railway";

    private const String NotImplementedDeclaration = "    private static readonly Error NotImplemented =";

    public IReadOnlyList<NewFile> CreateFiles() =>
        feature.Kind switch
        {
            FeatureKind.Query => [QueryFile, DtoFile, HandlerFile, .. QueryEndpointFiles],
            FeatureKind.PublicQuery => [PublicQueryFile, PublicDtoFile, HandlerFile],
            FeatureKind.EventHandler => [HandlerFile],
            _ => [CommandFile, HandlerFile, .. CommandEndpointFiles],
        };

    private IEnumerable<NewFile> CommandEndpointFiles =>
        feature.Method switch
        {
            EndpointMethod.Post or EndpointMethod.Put => [RequestFile, EndpointFile],
            EndpointMethod.Delete => [EndpointFile],
            _ => [],
        };

    private IEnumerable<NewFile> QueryEndpointFiles => feature.Method == EndpointMethod.Get ? [EndpointFile] : [];

    private NewFile CommandFile =>
        new(
            feature.InFeature($"{feature.Command}.cs"),
            CodeFile(
                feature.Namespace,
                [RailwayNamespace],
                RecordDeclaration("internal", feature.Command, $"{feature.Module.CommandMarker}<Unit>")));

    private NewFile QueryFile =>
        new(
            feature.InFeature($"{feature.Query}.cs"),
            CodeFile(
                feature.Namespace,
                [feature.Module.ContractsNamespace],
                RecordDeclaration("internal", feature.Query, QueryBaseType)));

    private NewFile DtoFile =>
        new(
            feature.InFeature($"{feature.Dto}.cs"),
            CodeFile(feature.Namespace, [], RecordDeclaration("internal", feature.Dto)));

    private NewFile PublicQueryFile =>
        new(
            feature.InPublicQuery($"{feature.Query}.cs"),
            CodeFile(feature.PublicQueryNamespace, [], RecordDeclaration("public", feature.Query, QueryBaseType)));

    private NewFile PublicDtoFile =>
        new(
            feature.InPublicQuery($"{feature.Dto}.cs"),
            CodeFile(feature.PublicQueryNamespace, [], RecordDeclaration("public", feature.Dto)));

    private String QueryBaseType => $"{feature.Module.QueryMarker}<{feature.Dto}>";

    private NewFile HandlerFile =>
        new(
            feature.InFeature($"{feature.Handler}.cs"),
            CodeFile(
                feature.Namespace,
                [.. HandlerUsings, RailwayNamespace],
                $$"""
                {{HandlerDeclaration}}
                {
                {{NotImplementedField}}

                {{HandleMethod}}
                }
                """));

    private IEnumerable<String> HandlerUsings =>
        feature.Kind switch
        {
            FeatureKind.Query => ["Tsikhanau.Monolith.Application.Queries"],
            FeatureKind.PublicQuery => ["Tsikhanau.Monolith.Application.Queries", feature.PublicQueryNamespace],
            FeatureKind.EventHandler => ["Tsikhanau.Monolith.Application.Events", HandledEvent.Namespace],
            _ => ["Tsikhanau.Monolith.Application.Commands"],
        };

    private EventDeclaration HandledEvent => feature.Event.Value;

    private String HandlerInterface =>
        feature.Kind switch
        {
            FeatureKind.Query or FeatureKind.PublicQuery => "IQueryHandler",
            FeatureKind.EventHandler => HandledEvent.HandlerInterface,
            _ => "ICommandHandler",
        };

    private String HandledType => feature.IsEventHandler ? HandledEvent.Name : feature.HandledRequest;

    private String HandledParameter =>
        feature.Kind switch
        {
            FeatureKind.Query or FeatureKind.PublicQuery => "query",
            FeatureKind.EventHandler => HandledEvent.ParameterName,
            _ => "command",
        };

    private IReadOnlyList<String> HandlerTypeArguments =>
        feature.IsEventHandler ? [HandledType] : [HandledType, feature.HandlerResult];

    private NewFile RequestFile =>
        new(
            feature.InFeature($"{feature.RequestRecord}.cs"),
            CodeFile(feature.Namespace, [], RecordDeclaration("internal", feature.RequestRecord)));

    private NewFile EndpointFile =>
        new(
            feature.InFeature($"{feature.Endpoint}.cs"),
            CodeFile(
                feature.Namespace,
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
                            .{{EndpointAnswer}}();
                }
                """));

    private IReadOnlyList<String> EndpointResults =>
        [feature.IsQuery ? $"Ok<{feature.Dto}>" : "NoContent", "ValidationProblem", "ProblemHttpResult"];

    private String EndpointAnswer => feature.IsQuery ? "ToOkAsync" : "ToNoContentAsync";

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

    private String NotImplementedFailure =>
        feature.IsQuery ? $"Result.Failure<{feature.Dto}>(NotImplemented)" : "Result.Failure(NotImplemented)";

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
            $"""            .{feature.FacadeMethod}(new {feature.HandledRequest}(), cancellationToken)""",
            $"""
                         .{feature.FacadeMethod}(
                             new {feature.HandledRequest}(),
                             cancellationToken)
             """);

    private String HandlerDeclaration
    {
        get
        {
            var baseType = $"{HandlerInterface}<{String.Join(", ", HandlerTypeArguments)}>";

            return FirstThatFits(
                $"internal sealed class {feature.Handler} : {baseType}",
                $"""
                 internal sealed class {feature.Handler}
                     : {baseType}
                 """,
                $"""
                 internal sealed class {feature.Handler}
                     : {HandlerInterface}<
                         {String.Join(",\n        ", HandlerTypeArguments)}>
                 """);
        }
    }

    private String HandleMethod
    {
        get
        {
            var head = $"public Task<Result<{feature.HandlerResult}>> Handle";
            var parameter = $"{HandledType} {HandledParameter}";

            return FirstThatFits(
                $"""
                     {head}({parameter}, CancellationToken cancellationToken) =>
                 {HandleBody}
                 """,
                $"""
                     {head}(
                         {parameter},
                         CancellationToken cancellationToken) =>
                 {HandleBody}
                 """);
        }
    }

    private String HandleBody =>
        FirstThatFits(
            $"        Task.FromResult({NotImplementedFailure});",
            $"""
                     Task.FromResult(
                         {NotImplementedFailure});
             """);

    private static String RecordDeclaration(String visibility, String name) => $"{visibility} sealed record {name};";

    private static String RecordDeclaration(String visibility, String name, String baseType) =>
        FirstThatFits(
            $"{visibility} sealed record {name} : {baseType};",
            $"""
             {visibility} sealed record {name}
                 : {baseType};
             """);

    private static String CodeFile(String codeNamespace, IReadOnlyList<String> usings, String body) =>
        $"{UsingBlock([.. usings.Where(usedNamespace => !Encloses(usedNamespace, codeNamespace))])}"
        + $"namespace {codeNamespace};\n\n{body}\n";

    private static Boolean Encloses(String outerNamespace, String codeNamespace) =>
        codeNamespace == outerNamespace || codeNamespace.StartsWith($"{outerNamespace}.", StringComparison.Ordinal);

    private static String UsingBlock(IReadOnlyList<String> usings) =>
        usings.Count == 0 ? String.Empty : $"{Usings(usings)}\n\n";
}
