using Tsikhanau.Monolith.Application.Commands;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.BlankInternalCommandName;

[InternalCommandName(" ")]
public sealed record BlankNamedInternalCommand(Guid Id) : InternalCommand(Id);
