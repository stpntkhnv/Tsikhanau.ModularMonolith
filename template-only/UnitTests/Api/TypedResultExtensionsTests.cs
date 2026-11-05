using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Hosting;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Api;

public sealed class TypedResultExtensionsTests
{
    private const String Secret = "the password of the orders database";

    private static readonly Error OrderRejected =
        new(ErrorKind.Validation, "Order.Rejected", "Order was rejected")
        {
            Inner = AggregateError.From(
            [
                Error.Validation("email", "Email is not valid"),
                Error.Unexpected("Orders.Database", Secret)
            ])
        };

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Validation_error_answers_its_messages_and_none_of_an_unexpected_error_inside_it()
    {
        await using var app = await StartProbeAppAsync();
        using var httpClient = app.GetTestClient();

        using var response = await httpClient.GetAsync("/probe", CancellationToken);
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        var answer = $"{(Int32)response.StatusCode} {DescribeProblem(body)}";

        answer.ShouldBe("400 Order.Rejected errors[]: Order was rejected; errors[email]: Email is not valid");
        body.ShouldNotContain(Secret);
    }

    private static async Task<WebApplication> StartProbeAppAsync()
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production });

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.AddHttpDefaults();

        var app = builder.Build();
        app.UseServiceDefaults();
        app.MapGet("/probe", RejectOrderAsync);
        await app.StartAsync(CancellationToken);
        return app;
    }

    private static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> RejectOrderAsync() =>
        Task
            .FromResult(Result<Unit>.Failure(OrderRejected))
            .ToNoContentAsync();

    private static String DescribeProblem(String body)
    {
        var problem = JsonSerializer.Deserialize<JsonElement>(body);
        return $"{problem.GetProperty("code")} {DescribeErrors(problem)}";
    }

    private static String DescribeErrors(JsonElement problem) =>
        String.Join(
            "; ",
            problem
                .GetProperty("errors")
                .EnumerateObject()
                .Select(field => $"errors[{field.Name}]: {String.Join(", ", field.Value.EnumerateArray())}"));
}
