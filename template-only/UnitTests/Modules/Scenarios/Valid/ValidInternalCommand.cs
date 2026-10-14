using Tsikhanau.Monolith.Application.Commands;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.Valid;

[InternalCommandName("valid")]
public sealed record ValidInternalCommand(Guid Id) : InternalCommand(Id);
