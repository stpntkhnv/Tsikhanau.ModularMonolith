using ModularMonolith.BuildingBlocks.Application.Commands;

namespace ModularMonolith.TestModules.Pings;

internal interface IPingsCommand<TResult> : ICommand<TResult>
    where TResult : notnull;
