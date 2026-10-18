using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.Dispatching;

internal sealed class CreatePingThenThrowCommandHandler(IPingRepository pingRepository, TimeProvider timeProvider)
    : ICommandHandler<CreatePingThenThrowCommand, Unit>
{
    public const String Failure = "Handler failed after adding a ping";

    public Task<Result<Unit>> Handle(CreatePingThenThrowCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(
            PingLabel
                .Create("thrown", "label")
                .Map(label => Ping.Create(PingId.From(command.PingId), label, timeProvider.GetUtcNow()))
                .Tap(pingRepository.Add)
                .Bind(Throw));

    private static Result<Unit> Throw(Ping _) =>
        throw new InvalidOperationException(Failure);
}
