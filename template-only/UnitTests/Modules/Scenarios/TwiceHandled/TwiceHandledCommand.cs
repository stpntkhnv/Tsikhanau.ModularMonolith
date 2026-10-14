using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.TwiceHandled;

public sealed record TwiceHandledCommand : ICommand<Unit>;
