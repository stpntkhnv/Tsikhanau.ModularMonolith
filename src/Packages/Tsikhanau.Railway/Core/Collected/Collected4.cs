namespace Tsikhanau.Railway;

public readonly struct Collected<T1, T2, T3, T4>
    where T1 : notnull
    where T2 : notnull
    where T3 : notnull
    where T4 : notnull
{
    private readonly Result<T1> _first;
    private readonly Result<T2> _second;
    private readonly Result<T3> _third;
    private readonly Result<T4> _fourth;
    private readonly Boolean _initialized;

    internal Collected(Result<T1> first, Result<T2> second, Result<T3> third, Result<T4> fourth)
    {
        _first = first;
        _second = second;
        _third = third;
        _fourth = fourth;
        _initialized = true;
    }

    public Collected<T1, T2, T3, T4, T5> And<T5>(Result<T5> next)
        where T5 : notnull
    {
        Guard.AgainstUncollected(_initialized);
        return new Collected<T1, T2, T3, T4, T5>(_first, _second, _third, _fourth, next);
    }

    public Result<TOut> Map<TOut>(Func<T1, T2, T3, T4, TOut> map)
        where TOut : notnull
    {
        Guard.AgainstNull(map);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        var second = _second;
        var third = _third;
        var fourth = _fourth;
        if (first.IsSuccess && second.IsSuccess && third.IsSuccess && fourth.IsSuccess)
        {
            return Result<TOut>.Success(map(
                first.Value,
                second.Value,
                third.Value,
                fourth.Value));
        }

        return Result<TOut>.Failure(Errors());
    }

    public Result<TOut> Bind<TOut>(Func<T1, T2, T3, T4, Result<TOut>> bind)
        where TOut : notnull
    {
        Guard.AgainstNull(bind);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        var second = _second;
        var third = _third;
        var fourth = _fourth;
        if (first.IsSuccess && second.IsSuccess && third.IsSuccess && fourth.IsSuccess)
        {
            return bind(
                first.Value,
                second.Value,
                third.Value,
                fourth.Value);
        }

        return Result<TOut>.Failure(Errors());
    }

    private Error Errors()
    {
        List<Error>? errors = null;
        ErrorMerge.Add(ref errors, _first);
        ErrorMerge.Add(ref errors, _second);
        ErrorMerge.Add(ref errors, _third);
        ErrorMerge.Add(ref errors, _fourth);
        return ErrorMerge.Merge(errors!);
    }
}
