namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static TOutput Match<T, TOutput>(
        this Result<T> result,
        Func<T, TOutput> onSuccess,
        Func<Error, TOutput> onFailure)
        where T : notnull
    {
        return result.IsSuccess
            ? onSuccess(result.Value)
            : onFailure(result.Error);
    }
}
