using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public static class UnitOfWorkErrors
{
    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "Concurrency.Conflict",
        "The data was changed by another operation");

    public static Error EventLoop(Int32 roundCount) =>
        Error.Unexpected("UnitOfWork.EventLoop", $"Domain events did not settle after {roundCount} rounds");
}
