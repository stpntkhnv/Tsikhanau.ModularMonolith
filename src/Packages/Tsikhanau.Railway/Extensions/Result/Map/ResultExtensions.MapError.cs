namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static Result<T> MapError<T>(
        this Result<T> result,
        Func<Error, Error> errorMapper)
        where T : notnull
    {
        return result.IsSuccess
            ? result
            : Result<T>.Failure(errorMapper(result.Error));
    }
}
