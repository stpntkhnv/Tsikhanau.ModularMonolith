namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static Result<TResult> Map<T, TResult>(
        this Result<T> result,
        Func<T, TResult> mapper)
        where T : notnull
        where TResult : notnull
    {
        return result.IsSuccess
            ? Result<TResult>.Success(mapper(result.Value))
            : Result<TResult>.Failure(result.Error);
    }
}
