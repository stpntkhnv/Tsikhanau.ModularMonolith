namespace Tsikhanau.Railway;

public static partial class Result
{
    public static Result<T> Try<T>(Func<T> func)
        where T : notnull
    {
        return Try(func, Error.FromException);
    }

    public static Result<T> Try<T>(
        Func<T> func,
        Func<Exception, Error> errorMapper)
        where T : notnull
    {
        Guard.AgainstNull(func);
        Guard.AgainstNull(errorMapper);

        T value;
        try
        {
            value = func();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Result<T>.Failure(errorMapper(exception));
        }

        return Result<T>.Success(value);
    }
}
