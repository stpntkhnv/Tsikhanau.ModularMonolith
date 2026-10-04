namespace ModularMonolith.BuildingBlocks.Application.Commands;

public interface ICommand<TResult>
    where TResult : notnull;
