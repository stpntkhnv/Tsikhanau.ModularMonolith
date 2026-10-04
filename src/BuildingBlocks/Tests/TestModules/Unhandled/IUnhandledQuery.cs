using ModularMonolith.BuildingBlocks.Contracts.Queries;

namespace ModularMonolith.TestModules.Unhandled;

internal interface IUnhandledQuery<TResult> : IQuery<TResult>
    where TResult : notnull;
