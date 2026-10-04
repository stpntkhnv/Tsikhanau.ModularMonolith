using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Application.Commands;

public abstract record InternalCommand(Guid Id) : ICommand<Unit>;
