namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static Result<T> TapError<T>(
        this Result<T> result,
        Action<Error> action)
        where T : notnull
    {
        if (result.IsFailure)
        {
            action(result.Error);
        }

        return result;
    }
}
