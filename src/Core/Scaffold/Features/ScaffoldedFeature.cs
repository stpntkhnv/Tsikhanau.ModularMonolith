using Tsikhanau.Monolith.Scaffold.Events;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed record ScaffoldedFeature(ScaffoldedModule Module, FeatureRequest Request)
{
    public const String TestProjectFolder = "src/Tests/IntegrationTests";

    private const String ReadVerb = "Get";

    public Optional<EventDeclaration> Event { get; init; } = Optional<EventDeclaration>.None();

    public String Name => Request.Name;

    public FeatureKind Kind => Request.Kind;

    public EndpointMethod Method => Request.Method;

    public Boolean IsQuery => Request.IsQuery;

    public Boolean IsEventHandler => Kind == FeatureKind.EventHandler;

    public Boolean HasRequestBody => Method is EndpointMethod.Post or EndpointMethod.Put;

    public String Namespace => $"{Module.Namespace}.Features.{Name}";

    public String FeaturesFolder => Module.InProject("Features");

    public String Folder => $"{FeaturesFolder}/{Name}";

    public String PublicQueriesFolder => Module.InContracts("Queries");

    public String PublicQueryFolder => $"{PublicQueriesFolder}/{Name}";

    public String PublicQueryNamespace => $"{Module.ContractsNamespace}.Queries.{Name}";

    public String Command => $"{Name}Command";

    public String Query => $"{Name}Query";

    public String InternalCommand => $"{Name}InternalCommand";

    public Optional<String> InternalCommandName =>
        Kind == FeatureKind.InternalCommand ? Name.ToKebabCase() : Optional<String>.None();

    public String Dto => $"{ReadNoun}Dto";

    public String HandledRequest =>
        Kind switch
        {
            FeatureKind.Query or FeatureKind.PublicQuery => Query,
            FeatureKind.InternalCommand => InternalCommand,
            _ => Command,
        };

    public String Handler =>
        Event.Match(handledEvent => $"{Name}{handledEvent.HandlerSuffix}", () => $"{HandledRequest}Handler");

    public Optional<String> Neighbour =>
        Event
            .Where(handledEvent => handledEvent.Kind == EventKind.Integration)
            .Map(handledEvent => handledEvent.OwnerModule);

    public String ChosenNames =>
        Event.Match(
            handledEvent => $"feature name '{Name}' with event {handledEvent.Name}",
            () => $"feature name '{Name}'{ChosenRoute}");

    public String HandlerResult => IsQuery ? Dto : "Unit";

    public String FacadeMethod => IsQuery ? "ExecuteQueryAsync" : "ExecuteCommandAsync";

    public String RequestRecord => $"{Name}Request";

    public String Endpoint => $"{Name}Endpoint";

    public EndpointRoute Route => Request.Route.GetValueOrDefault(DefaultRoute);

    public String RouteChoice => Request.Route.HasValue ? "--route" : "feature name";

    public String ShorterChoice => Request.Route.HasValue ? "name or --route" : "name";

    public String RouteName => $"{Module.Name}.{Name}";

    public String HttpPath => $"/{Module.Route}{Route.Path}";

    public String HttpPathWithSampleIds => $"/{Module.Route}{Route.PathWithSampleIds}";

    public String HttpMethod => $"{Method}".ToUpperInvariant();

    public String MapMethod => $"Map{Method}";

    public String FacadeParameter => Module.ModuleClass.ToCamelCase();

    public String QueryFacadeParameter => $"{Module.Name}QueryFacade".ToCamelCase();

    public String NotImplementedCode => $"{Name}.NotImplemented";

    public String NotImplementedMessage => $"{Name} in {Module.Name} is not implemented";

    public String TestClass => $"{Name}Tests";

    public String TestNamespace => $"{Module.App.RootNamespace}.IntegrationTests.Modules.{Module.Name}";

    public String TestPath => $"{TestProjectFolder}/Modules/{Module.Name}/{TestClass}.cs";

    private String ReadNoun => Name.StartsWithWord(ReadVerb) ? Name[ReadVerb.Length..] : Name;

    private EndpointRoute DefaultRoute => EndpointRoute.From($"/{(IsQuery ? ReadNoun : Name).ToKebabCase()}");

    private String ChosenRoute => Request.Route.Match(route => $" with route '{route.Path}'", () => String.Empty);

    public String InFeature(String fileName) => $"{Folder}/{fileName}";

    public String InPublicQuery(String fileName) => $"{PublicQueryFolder}/{fileName}";

    public FacadeShape GetRequiredShape(FacadeShape currentShape) =>
        Kind switch
        {
            FeatureKind.Query or FeatureKind.PublicQuery => currentShape.WithQueries(),
            FeatureKind.EventHandler or FeatureKind.InternalCommand => currentShape,
            _ => currentShape.WithCommands(),
        };
}
