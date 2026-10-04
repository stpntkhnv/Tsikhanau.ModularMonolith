using ModularMonolith.BuildingBlocks.Application.Commands;

namespace ModularMonolith.TestModules.Pongs;

internal interface IPongsCommand<TResult> : ICommand<TResult>
    where TResult : notnull;
