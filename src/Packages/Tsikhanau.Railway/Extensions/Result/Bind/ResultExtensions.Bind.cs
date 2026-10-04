namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static Result<TResult> Bind<T, TResult>(
        this Result<T> result,
        Func<T, Result<TResult>> binder)
        where T : notnull
        where TResult : notnull
    {
        return result.IsSuccess
            ? binder(result.Value)
            : Result<TResult>.Failure(result.Error);
    }
}
