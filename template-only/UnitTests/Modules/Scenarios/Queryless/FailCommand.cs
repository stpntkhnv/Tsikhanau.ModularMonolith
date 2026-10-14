using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.Queryless;

public sealed record FailCommand(Error Error) : ICommand<Unit>;
