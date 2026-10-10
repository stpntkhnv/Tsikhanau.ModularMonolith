using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.TestModules.Pings.Features.ProduceError;
using Tsikhanau.Monolith.TestModules.Pongs.Features;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.App;

[Collection(TestAppCollection.Name)]
public sealed class NotImplementedReportTests(TestAppFixture testAppFixture) : TestAppTest(testAppFixture)
{
    private const String FailureCode = "Ping.NotProcessed";

    private const String FailureMessage = "Ping could not be processed";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Status_assertion_prints_the_error_only_when_it_carries_the_body()
    {
        using var httpClient = App.CreateClient();

        var response = await httpClient.GetAsync("/pings/errors/Failure", CancellationToken);
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        var statusOnly = Should.Throw<ShouldAssertException>(
            () => response.StatusCode.ShouldBe(HttpStatusCode.NoContent));

        var statusWithBody = Should.Throw<ShouldAssertException>(
            () => response.StatusCode.ShouldBe(HttpStatusCode.NoContent, body));

        statusOnly.Message.ShouldNotContain(FailureCode);
        statusWithBody.Message.ShouldContain($$"""
            "detail":"{{FailureMessage}}","code":"{{FailureCode}}"
            """);
    }

    [Fact]
    public async Task Result_assertion_prints_the_error_only_when_it_compares_the_result()
    {
        var pingsModule = App.Services.GetRequiredService<IPingsModule>();

        var result = await pingsModule.ExecuteQueryAsync(
            new ProduceErrorQuery(ProducedErrorKind.Failure),
            CancellationToken);

        var successFlag = Should.Throw<ShouldAssertException>(() => result.IsSuccess.ShouldBeTrue());
        var successResult = Should.Throw<ShouldAssertException>(() => result.ShouldBe(Result.Success()));

        successFlag.Message.ShouldNotContain(FailureCode);
        successResult.Message.ShouldContain($"Failure([{FailureCode}] {FailureMessage})");
    }

    [Fact]
    public async Task Queue_run_prints_the_last_error_of_a_failed_handler()
    {
        const String label = "refused-in-the-queue";
        var pongTestHooks = App.Services.GetRequiredService<PongTestHooks>();
        var integrationEvent = new PingCreatedIntegrationEvent(
            App.Time.GetUtcNow(),
            Guid.CreateVersion7(),
            label,
            Optional<String>.None());

        pongTestHooks.FailFor = pongTestHooks.FailFor.Add(label);
        await App.PublishAsync(integrationEvent);

        var exception = await Should.ThrowAsync<InvalidOperationException>(App.ProcessQueuesAsync);

        exception.Message.ShouldBe(
            "Queues hold failed or retrying messages: "
            + "Pongs inbox_messages pings.ping-created after 1 attempts: Pong.Rejected: Pong refused this ping. "
            + "A test that expects a failure processes queues with ProcessQueuesAllowingFailuresAsync");
    }
}
