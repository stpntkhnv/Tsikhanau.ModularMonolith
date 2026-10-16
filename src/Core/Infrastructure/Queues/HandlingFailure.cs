using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

internal readonly partial record struct HandlingFailure(
    String Code,
    String Detail,
    Boolean IsPermanent,
    Exception? Exception)
{
    private const String CodeSeparator = ": ";

    public static readonly HandlingFailure ChangedStateOutsideTransaction = new(
        "Handler.ChangedStateOutsideTransaction",
        "The handler changed module state outside a transaction",
        IsPermanent: true,
        Exception: null);

    public String Reason => $"{Code}{CodeSeparator}{Detail}";

    public String CodeAndExceptionType => Exception is null ? Code : $"{Code} ({Exception.GetType().Name})";

    public static Optional<String> FindCode(String lastError)
    {
        var textBeforeSeparator = lastError
            .Split(CodeSeparator, count: 2)
            .First();

        return CodePattern.IsMatch(textBeforeSeparator) ? textBeforeSeparator : Optional<String>.None();
    }

    public static HandlingFailure From(Error error) =>
        new(error.Code, error.Message, IsPermanent: !IsTransient(error), Exception: null);

    public static HandlingFailure From(Exception exception) =>
        new("Handler.Exception", exception.Message, IsPermanent: false, exception);

    public static HandlingFailure TimedOut(TimeSpan handlerTimeout, Exception exception) =>
        new("Handler.Timeout", $"Timed out after {handlerTimeout}", IsPermanent: false, exception);

    public static HandlingFailure Abandoned(Int32 attempts) =>
        new(
            "Handler.Abandoned",
            $"Gave up after {attempts - 1} attempts, the last did not finish",
            IsPermanent: true,
            Exception: null);

    private static Boolean IsTransient(Error error) =>
        (error.Kind == ErrorKind.Unexpected && error.Code != UnitOfWorkErrors.EventLoopCode)
        || error.Code == UnitOfWorkErrors.ConcurrencyConflict.Code;

    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+\z")]
    private static partial Regex CodePattern { get; }
}
