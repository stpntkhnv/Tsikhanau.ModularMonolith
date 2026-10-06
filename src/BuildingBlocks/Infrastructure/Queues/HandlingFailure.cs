using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Queues;

internal readonly record struct HandlingFailure(String Reason, Boolean IsPermanent, Exception? Exception)
{
    public static readonly HandlingFailure ChangedStateOutsideTransaction =
        new("The handler changed module state outside a transaction", IsPermanent: true, Exception: null);

    public static HandlingFailure From(Error error) =>
        new($"{error.Code}: {error.Message}", IsPermanent: !IsTransient(error), Exception: null);

    public static HandlingFailure From(Exception exception) =>
        new(exception.Message, IsPermanent: false, exception);

    public static HandlingFailure TimedOut(TimeSpan handlerTimeout, Exception exception) =>
        new($"Timed out after {handlerTimeout}", IsPermanent: false, exception);

    public static HandlingFailure Abandoned(Int32 attempts) =>
        new($"Gave up after {attempts - 1} attempts, the last did not finish", IsPermanent: true, Exception: null);

    private static Boolean IsTransient(Error error) =>
        (error.Kind == ErrorKind.Unexpected && error.Code != UnitOfWorkErrors.EventLoopCode)
        || error.Code == UnitOfWorkErrors.ConcurrencyConflict.Code;
}
