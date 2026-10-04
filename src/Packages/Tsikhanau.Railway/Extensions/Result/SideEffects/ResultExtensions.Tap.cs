namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static Result<T> Tap<T>(
        this Result<T> result,
        Action<T> action)
        where T : notnull
    {
        if (result.IsSuccess)
        {
            action(result.Value);
        }

        return result;
    }
}
