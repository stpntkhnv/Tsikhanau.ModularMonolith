using Tsikhanau.Monolith.Scaffold.Events;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Transactions;
using static Tsikhanau.Monolith.Scaffold.Modules.CodeLayout;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed class FeatureTestTemplate(ScaffoldedFeature feature)
{
    private const String Indentation = "        ";

    private const String RailwayNamespace = "Tsikhanau.Railway";

    private const String DependencyInjectionNamespace = "Microsoft.Extensions.DependencyInjection";

    private const String InboxNamespace = "Tsikhanau.Monolith.Infrastructure.Inbox";

    private const String InternalCommandsNamespace = "Tsikhanau.Monolith.Infrastructure.InternalCommands";

    private const String MessageIdFromTheClock = "Guid.CreateVersion7(App.Time.GetUtcNow())";

    private const String CancellationTokenProperty =
        "    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;\n\n";

    public String TestMethod => Test.Method;

    public NewFile CreateFile() => new(feature.TestPath, Render(Test));

    private FeatureTest Test =>
        (feature.Kind, feature.Method) switch
        {
            (FeatureKind.EventHandler, _) when HandledEvent.Kind == EventKind.Domain => DomainEventTest,
            (FeatureKind.EventHandler, _) => IntegrationEventTest,
            (FeatureKind.PublicQuery, _) => QueryFacadeTest,
            (FeatureKind.InternalCommand, _) => InternalCommandTest,
            (_, EndpointMethod.None) => FacadeTest,
            _ => HttpTest,
        };

    private String Sentence => ToSentence(feature.Name);

    private FeatureTest FacadeTest =>
        new(
            $"{Sentence}_succeeds_through_the_facade",
            [feature.Module.Namespace, feature.Namespace, DependencyInjectionNamespace, RailwayNamespace],
            UsesCancellationToken: true,
            CallThroughFacade(feature.FacadeParameter, feature.Module.FacadeContract));

    private FeatureTest QueryFacadeTest =>
        new(
            $"{Sentence}_succeeds_through_the_query_facade",
            [
                feature.Module.ContractsNamespace,
                feature.PublicQueryNamespace,
                DependencyInjectionNamespace,
                RailwayNamespace,
            ],
            UsesCancellationToken: true,
            CallThroughFacade(feature.QueryFacadeParameter, feature.Module.QueryFacade));

    private String ExpectedResult => feature.IsQuery ? $"Result.Success(new {feature.Dto}())" : "Result.Success()";

    private String HttpStatus => feature.IsQuery ? "OK" : "NoContent";

    private FeatureTest HttpTest =>
        new(
            $"{Sentence}_answers_{HttpStatus.ToSnakeCase()}",
            ["System.Net", .. HttpUsings],
            UsesCancellationToken: true,
            Lines(
            [
                $"{Indentation}using var httpClient = App.CreateClient();",
                String.Empty,
                SendRequest,
                .. BlankLineAfter(SendRequest),
                $"{Indentation}var body = await response.Content.ReadAsStringAsync(CancellationToken);",
                String.Empty,
                $"{Indentation}response.StatusCode.ShouldBe(HttpStatusCode.{HttpStatus}, body);",
            ]));

    private IEnumerable<String> HttpUsings => feature.HasRequestBody ? ["System.Net.Http.Json"] : [];

    private String SendRequest =>
        AwaitCall("response", $"httpClient.{HttpCall}", [.. HttpArguments, "CancellationToken"]);

    private String HttpCall => feature.HasRequestBody ? $"{feature.Method}AsJsonAsync" : $"{feature.Method}Async";

    private IEnumerable<String> HttpArguments =>
        feature.HasRequestBody ? [RequestPath, "new { }"] : [RequestPath];

    private String RequestPath => $"\"{feature.HttpPathWithSampleIds}\"";

    private EventDeclaration HandledEvent => feature.Event.Value;

    private FeatureTest DomainEventTest =>
        new(
            $"{ToSentence(HandledEvent.Name)}_is_handled",
            [feature.Module.Namespace, .. HandledEvent.Usings, RailwayNamespace],
            UsesCancellationToken: false,
            Lines(
            [
                BuildEvent,
                String.Empty,
                DispatchEvent,
                .. BlankLineAfter(DispatchEvent),
                $"{Indentation}await App.ProcessQueuesAsync();",
                String.Empty,
                $"{Indentation}dispatched.ShouldBe(Result.Success());",
            ]));

    private FeatureTest IntegrationEventTest =>
        new(
            $"{ToSentence(HandledEvent.Name)}_is_handled_through_the_inbox",
            [.. HandledEvent.Usings, PersistenceNamespace, InboxNamespace],
            UsesCancellationToken: false,
            Lines(
                BuildEvent,
                String.Empty,
                $"{Indentation}await App.PublishAsync({HandledEvent.ParameterName});",
                $"{Indentation}await App.ProcessQueuesAsync();",
                ReadQueueRows("inboxRows", "InboxMessage.Table"),
                String.Empty,
                AssertOnlyRowIsProcessed("inboxRows", "inboxRow", HandledEvent.ParameterName)));

    private FeatureTest InternalCommandTest =>
        new(
            $"{Sentence}_runs_when_the_queues_run",
            [feature.Module.Namespace, feature.Namespace, PersistenceNamespace, InternalCommandsNamespace],
            UsesCancellationToken: false,
            Lines(
                Construct("internalCommand", feature.InternalCommand, [MessageIdFromTheClock]),
                String.Empty,
                $"{Indentation}await App.EnqueueAsync<{feature.Module.FacadeContract}>(internalCommand);",
                $"{Indentation}await App.ProcessQueuesAsync();",
                ReadQueueRows("internalCommandRows", "InternalCommandMessage.Table"),
                String.Empty,
                AssertOnlyRowIsProcessed("internalCommandRows", "internalCommandRow", "internalCommand")));

    private String PersistenceNamespace => $"{feature.Module.Namespace}.Infrastructure.Persistence";

    private String BuildEvent =>
        Construct(
            HandledEvent.ParameterName,
            HandledEvent.Name,
            [.. HandledEvent.Members.Select(member => member.Expression)]);

    private String DispatchEvent =>
        AwaitCall(
            "dispatched",
            $"App.DispatchAsync<{feature.Module.FacadeContract}>",
            [HandledEvent.ParameterName]);

    private String ReadQueueRows(String variable, String table) =>
        AwaitCall(variable, "App.Database.ReadQueueRowsAsync", [$"{feature.Module.DbContext}.SchemaName", table]);

    private String CallThroughFacade(String facadeVariable, String facadeType) =>
        Lines(
            Resolve(facadeVariable, facadeType),
            String.Empty,
            AwaitCall(
                "result",
                $"{facadeVariable}.{feature.FacadeMethod}",
                [$"new {feature.HandledRequest}()", "CancellationToken"]),
            String.Empty,
            $"{Indentation}result.ShouldBe({ExpectedResult});");

    private String TestClassDeclaration =>
        FirstThatFits(
            $"public sealed class {feature.TestClass}(AppFixture appFixture) : AppTest(appFixture)",
            $"""
             public sealed class {feature.TestClass}(AppFixture appFixture)
                 : AppTest(appFixture)
             """);

    private String Render(FeatureTest test) =>
        $$"""
        {{Usings([.. test.Usings, $"{feature.Module.App.RootNamespace}.Testing.App"])}}

        namespace {{feature.TestNamespace}};

        {{TestClassDeclaration}}
        {
        {{DeclareCancellationToken(test)}}    [Fact]
            public async Task {{test.Method}}()
            {
        {{test.Body}}
            }
        }

        """;

    private static String DeclareCancellationToken(FeatureTest test) =>
        test.UsesCancellationToken ? CancellationTokenProperty : String.Empty;

    private static String Resolve(String variable, String serviceType)
    {
        var resolveCall = $"App.Services.GetRequiredService<{serviceType}>()";

        return FirstThatFits(
            $"{Indentation}var {variable} = {resolveCall};",
            $"""
             {Indentation}var {variable} =
             {Indentation}    {resolveCall};
             """);
    }

    private static String AwaitCall(String variable, String call, IReadOnlyList<String> arguments) =>
        AssignCall(variable, $"await {call}", arguments);

    private static String Construct(String variable, String type, IReadOnlyList<String> arguments) =>
        AssignCall(variable, $"new {type}", arguments);

    private static String AssignCall(String variable, String call, IReadOnlyList<String> arguments) =>
        FirstThatFits(
            $"{Indentation}var {variable} = {call}({String.Join(", ", arguments)});",
            $"""
             {Indentation}var {variable} = {call}(
             {Indentation}    {String.Join($",\n{Indentation}    ", arguments)});
             """);

    private static String AssertOnlyRowIsProcessed(String rowsVariable, String rowVariable, String messageVariable) =>
        Lines(
            $"{Indentation}{rowsVariable}",
            $"{Indentation}    .Select({rowVariable} => ({rowVariable}.Id, {rowVariable}.IsProcessed))",
            $"{Indentation}    .ShouldBe([({messageVariable}.Id, true)]);");

    private static IEnumerable<String> BlankLineAfter(String statement) =>
        statement.Contains('\n', StringComparison.Ordinal) ? [String.Empty] : [];

    private static String Lines(params IEnumerable<String> lines) => String.Join('\n', lines);

    private static String ToSentence(String pascalName)
    {
        var words = pascalName.ToSnakeCase();
        return $"{Char.ToUpperInvariant(words[0])}{words[1..]}";
    }
}
