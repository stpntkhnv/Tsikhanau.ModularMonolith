using ModularMonolith.BuildingBlocks.Contracts.Queries;

namespace ModularMonolith.TestModules.Pongs.Contracts;

public interface IPongsQuery<TResult> : IQuery<TResult>
    where TResult : notnull;
