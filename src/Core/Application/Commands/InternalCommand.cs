using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Application.Commands;

public abstract record InternalCommand(Guid Id) : ICommand<Unit>;
