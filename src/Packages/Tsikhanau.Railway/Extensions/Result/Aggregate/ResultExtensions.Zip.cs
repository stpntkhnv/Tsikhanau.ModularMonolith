namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static Result<(T1, T2)> Zip<T1, T2>(
        this Result<T1> first,
        Result<T2> second)
        where T1 : notnull
        where T2 : notnull
    {
        if (first.IsFailure)
            return Result<(T1, T2)>.Failure(first.Error);
        if (second.IsFailure)
            return Result<(T1, T2)>.Failure(second.Error);
        return Result<(T1, T2)>.Success((first.Value, second.Value));
    }
}
