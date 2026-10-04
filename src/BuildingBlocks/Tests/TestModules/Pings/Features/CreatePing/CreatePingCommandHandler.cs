using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.CreatePing;

internal sealed class CreatePingCommandHandler(IPingRepository pingRepository, TimeProvider timeProvider)
    : ICommandHandler<CreatePingCommand, Guid>
{
    public Task<Result<Guid>> Handle(CreatePingCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(
            CreatePing(command)
                .Tap(pingRepository.Add)
                .Map(ping => ping.Id.Value));

    private Result<Ping> CreatePing(CreatePingCommand command) =>
        Result
            .Collect(PingId.Parse(command.PingId, "pingId"))
            .And(PingLabel.Create(command.Label, "label"))
            .Map((pingId, label) => Ping.Create(pingId, label, timeProvider.GetUtcNow()));
}
