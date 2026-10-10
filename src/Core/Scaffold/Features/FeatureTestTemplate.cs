using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Transactions;
using static Tsikhanau.Monolith.Scaffold.Modules.CodeLayout;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed class FeatureTestTemplate(ScaffoldedFeature feature)
{
    private const String Indentation = "        ";

    private const String RailwayNamespace = "Tsikhanau.Railway";

    private const String DependencyInjectionNamespace = "Microsoft.Extensions.DependencyInjection";

    public String TestMethod => Test.Method;

    public NewFile CreateFile() => new(feature.TestPath, Render(Test));

    private FeatureTest Test =>
        (feature.Kind, feature.Method) switch
        {
            (FeatureKind.PublicQuery, _) => QueryFacadeTest,
            (_, EndpointMethod.None) => FacadeTest,
            _ => HttpTest,
        };

    private String Sentence => ToSentence(feature.Name);

    private FeatureTest FacadeTest =>
        new(
            $"{Sentence}_succeeds_through_the_facade",
            [feature.Module.Namespace, feature.Namespace, DependencyInjectionNamespace, RailwayNamespace],
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
            CallThroughFacade(feature.QueryFacadeParameter, feature.Module.QueryFacade));

    private String ExpectedResult => feature.IsQuery ? $"Result.Success(new {feature.Dto}())" : "Result.Success()";

    private String HttpStatus => feature.IsQuery ? "OK" : "NoContent";

    private FeatureTest HttpTest =>
        new(
            $"{Sentence}_answers_{HttpStatus.ToSnakeCase()}",
            ["System.Net", .. HttpUsings],
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
        feature.HasRequestBody ? [$"\"{feature.HttpPath}\"", "new { }"] : [$"\"{feature.HttpPath}\""];

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
            private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

            [Fact]
            public async Task {{test.Method}}()
            {
        {{test.Body}}
            }
        }

        """;

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
        FirstThatFits(
            $"{Indentation}var {variable} = await {call}({String.Join(", ", arguments)});",
            $"""
             {Indentation}var {variable} = await {call}(
             {Indentation}    {String.Join($",\n{Indentation}    ", arguments)});
             """);

    private static IEnumerable<String> BlankLineAfter(String statement) =>
        statement.Contains('\n', StringComparison.Ordinal) ? [String.Empty] : [];

    private static String Lines(params IEnumerable<String> lines) => String.Join('\n', lines);

    private static String ToSentence(String pascalName)
    {
        var words = pascalName.ToSnakeCase();
        return $"{Char.ToUpperInvariant(words[0])}{words[1..]}";
    }
}
