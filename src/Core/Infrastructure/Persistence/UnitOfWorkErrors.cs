using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

public static class UnitOfWorkErrors
{
    public const String EventLoopCode = "UnitOfWork.EventLoop";

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "Concurrency.Conflict",
        "The data was changed by another operation");

    public static Error EventLoop(Int32 roundCount) =>
        Error.Unexpected(EventLoopCode, $"Domain events did not settle after {roundCount} rounds");
}
