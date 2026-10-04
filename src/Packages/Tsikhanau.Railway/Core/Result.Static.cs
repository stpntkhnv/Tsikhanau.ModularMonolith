namespace Tsikhanau.Railway;

public static partial class Result
{
    public static Result<T> Success<T>(T value) where T : notnull => Result<T>.Success(value);

    public static Result<Unit> Success() => Result<Unit>.Success(Unit.Value);

    public static Result<T> Failure<T>(Error error) where T : notnull => Result<T>.Failure(error);

    public static Result<Unit> Failure(Error error) => Result<Unit>.Failure(error);
}
