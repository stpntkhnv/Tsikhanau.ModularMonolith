using Tsikhanau.Monolith.Application.Commands;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.UnnamedInternalCommand;

public sealed record UnnamedInternalCommand(Guid Id) : InternalCommand(Id);
