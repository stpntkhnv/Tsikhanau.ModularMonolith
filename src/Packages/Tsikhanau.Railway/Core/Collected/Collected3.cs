namespace Tsikhanau.Railway;

public readonly struct Collected<T1, T2, T3>
    where T1 : notnull
    where T2 : notnull
    where T3 : notnull
{
    private readonly Result<T1> _first;
    private readonly Result<T2> _second;
    private readonly Result<T3> _third;
    private readonly Boolean _initialized;

    internal Collected(Result<T1> first, Result<T2> second, Result<T3> third)
    {
        _first = first;
        _second = second;
        _third = third;
        _initialized = true;
    }

    public Collected<T1, T2, T3, T4> And<T4>(Result<T4> next)
        where T4 : notnull
    {
        Guard.AgainstUncollected(_initialized);
        return new Collected<T1, T2, T3, T4>(_first, _second, _third, next);
    }

    public Result<TOut> Map<TOut>(Func<T1, T2, T3, TOut> map)
        where TOut : notnull
    {
        Guard.AgainstNull(map);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        var second = _second;
        var third = _third;
        if (first.IsSuccess && second.IsSuccess && third.IsSuccess)
        {
            return Result<TOut>.Success(map(
                first.Value,
                second.Value,
                third.Value));
        }

        return Result<TOut>.Failure(Errors());
    }

    public Result<TOut> Bind<TOut>(Func<T1, T2, T3, Result<TOut>> bind)
        where TOut : notnull
    {
        Guard.AgainstNull(bind);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        var second = _second;
        var third = _third;
        if (first.IsSuccess && second.IsSuccess && third.IsSuccess)
        {
            return bind(
                first.Value,
                second.Value,
                third.Value);
        }

        return Result<TOut>.Failure(Errors());
    }

    private Error Errors()
    {
        List<Error>? errors = null;
        ErrorMerge.Add(ref errors, _first);
        ErrorMerge.Add(ref errors, _second);
        ErrorMerge.Add(ref errors, _third);
        return ErrorMerge.Merge(errors!);
    }
}
