namespace Tsikhanau.Railway;

public readonly struct Collected<T1, T2>
    where T1 : notnull
    where T2 : notnull
{
    private readonly Result<T1> _first;
    private readonly Result<T2> _second;
    private readonly Boolean _initialized;

    internal Collected(Result<T1> first, Result<T2> second)
    {
        _first = first;
        _second = second;
        _initialized = true;
    }

    public Collected<T1, T2, T3> And<T3>(Result<T3> next)
        where T3 : notnull
    {
        Guard.AgainstUncollected(_initialized);
        return new Collected<T1, T2, T3>(_first, _second, next);
    }

    public Result<TOut> Map<TOut>(Func<T1, T2, TOut> map)
        where TOut : notnull
    {
        Guard.AgainstNull(map);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        var second = _second;
        if (first.IsSuccess && second.IsSuccess)
        {
            return Result<TOut>.Success(map(first.Value, second.Value));
        }

        return Result<TOut>.Failure(Errors());
    }

    public Result<TOut> Bind<TOut>(Func<T1, T2, Result<TOut>> bind)
        where TOut : notnull
    {
        Guard.AgainstNull(bind);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        var second = _second;
        if (first.IsSuccess && second.IsSuccess)
        {
            return bind(first.Value, second.Value);
        }

        return Result<TOut>.Failure(Errors());
    }

    private Error Errors()
    {
        List<Error>? errors = null;
        ErrorMerge.Add(ref errors, _first);
        ErrorMerge.Add(ref errors, _second);
        return ErrorMerge.Merge(errors!);
    }
}
