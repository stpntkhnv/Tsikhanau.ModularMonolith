namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T, Boolean> predicate,
        Error error)
        where T : notnull
    {
        if (result.IsFailure || predicate(result.Value))
        {
            return result;
        }

        return Result<T>.Failure(error);
    }

    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T, Boolean> predicate,
        Func<T, Error> errorFactory)
        where T : notnull
    {
        if (result.IsFailure || predicate(result.Value))
        {
            return result;
        }

        return Result<T>.Failure(errorFactory(result.Value));
    }
}
