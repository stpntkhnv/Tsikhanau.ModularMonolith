using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Transactions;
using static Tsikhanau.Monolith.Scaffold.Modules.CodeLayout;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed class FeatureTestTemplate(ScaffoldedFeature feature)
{
    private const String Indentation = "        ";

    private const String RailwayNamespace = "Tsikhanau.Railway";

    public String TestMethod => Test.Method;

    public NewFile CreateFile() => new(feature.TestPath, Render(Test));

    private FeatureTest Test => feature.Method == EndpointMethod.None ? FacadeTest : HttpTest;

    private String Sentence => ToSentence(feature.Name);

    private FeatureTest FacadeTest =>
        new(
            $"{Sentence}_succeeds_through_the_facade",
            [feature.Module.Namespace, feature.Namespace, "Microsoft.Extensions.DependencyInjection", RailwayNamespace],
            Lines(
                ResolveFacade,
                String.Empty,
                AwaitCall(
                    "result",
                    $"{feature.FacadeParameter}.ExecuteCommandAsync",
                    [$"new {feature.Command}()", "CancellationToken"]),
                String.Empty,
                $"{Indentation}result.ShouldBe(Result.Success());"));

    private FeatureTest HttpTest =>
        new(
            $"{Sentence}_answers_no_content",
            ["System.Net", .. HttpUsings],
            Lines(
            [
                $"{Indentation}using var httpClient = App.CreateClient();",
                String.Empty,
                SendRequest,
                .. BlankLineAfter(SendRequest),
                $"{Indentation}var body = await response.Content.ReadAsStringAsync(CancellationToken);",
                String.Empty,
                $"{Indentation}response.StatusCode.ShouldBe(HttpStatusCode.NoContent, body);",
            ]));

    private IEnumerable<String> HttpUsings => feature.HasRequestBody ? ["System.Net.Http.Json"] : [];

    private String SendRequest =>
        AwaitCall("response", $"httpClient.{HttpCall}", [.. HttpArguments, "CancellationToken"]);

    private String HttpCall => feature.HasRequestBody ? $"{feature.Method}AsJsonAsync" : $"{feature.Method}Async";

    private IEnumerable<String> HttpArguments =>
        feature.HasRequestBody ? [$"\"{feature.HttpPath}\"", "new { }"] : [$"\"{feature.HttpPath}\""];

    private String ResolveFacade =>
        FirstThatFits(
            $"{Indentation}var {feature.FacadeParameter} = {ResolveCall};",
            $"""
             {Indentation}var {feature.FacadeParameter} =
             {Indentation}    {ResolveCall};
             """);

    private String ResolveCall => $"App.Services.GetRequiredService<{feature.Module.FacadeContract}>()";

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
