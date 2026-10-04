using ModularMonolith.BuildingBlocks.Contracts.Queries;

namespace ModularMonolith.TestModules.Pings.Contracts;

public interface IPingsQuery<TResult> : IQuery<TResult>
    where TResult : notnull;
