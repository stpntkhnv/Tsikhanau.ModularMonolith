using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;

internal sealed class CreatePingCommandHandler(IPingRepository pingRepository, TimeProvider timeProvider)
    : ICommandHandler<CreatePingCommand, Guid>
{
    public Task<Result<Guid>> Handle(CreatePingCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(
            CreatePing(command)
                .Tap(pingRepository.Add)
                .Map(ping => ping.Id.Value));

    private static Result<Optional<PingLabel>> CreateNickname(Optional<String> nickname) =>
        nickname.Match(
            givenNickname => PingLabel
                .Create(givenNickname, "nickname")
                .Map(Optional<PingLabel>.Some),
            () => Result.Success(Optional<PingLabel>.None()));

    private static Result<Ping> GiveNicknameWhenPresent(Ping ping, Optional<PingLabel> nickname) =>
        nickname.Match(
            givenNickname => ping
                .GiveNickname(givenNickname)
                .Map(_ => ping),
            () => Result.Success(ping));

    private Result<Ping> CreatePing(CreatePingCommand command) =>
        Result
            .Collect(PingId.Parse(command.PingId, "pingId"))
            .And(PingLabel.Create(command.Label, "label"))
            .And(CreateNickname(command.Nickname))
            .Bind((pingId, label, nickname) => GiveNicknameWhenPresent(
                Ping.Create(pingId, label, timeProvider.GetUtcNow()),
                nickname));
}
