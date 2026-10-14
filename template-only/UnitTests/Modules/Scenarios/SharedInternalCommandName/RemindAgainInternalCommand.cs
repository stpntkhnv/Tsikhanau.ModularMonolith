using Tsikhanau.Monolith.Application.Commands;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.SharedInternalCommandName;

[InternalCommandName("remind")]
public sealed record RemindAgainInternalCommand(Guid Id) : InternalCommand(Id);
