using ModularMonolith.BuildingBlocks.Application.Commands;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios.Unhandled;

public sealed record UnhandledCommand : ICommand<Unit>;
