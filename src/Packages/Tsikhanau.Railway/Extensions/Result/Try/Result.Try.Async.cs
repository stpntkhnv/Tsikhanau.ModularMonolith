namespace Tsikhanau.Railway;

public static partial class Result
{
    public static Task<Result<T>> TryAsync<T>(Func<Task<T>> funcAsync)
        where T : notnull
    {
        return TryAsync(funcAsync, Error.FromException);
    }

    public static async Task<Result<T>> TryAsync<T>(
        Func<Task<T>> funcAsync,
        Func<Exception, Error> errorMapper)
        where T : notnull
    {
        Guard.AgainstNull(funcAsync);
        Guard.AgainstNull(errorMapper);

        T value;
        try
        {
            value = await funcAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Result<T>.Failure(errorMapper(exception));
        }

        return Result<T>.Success(value);
    }
}
