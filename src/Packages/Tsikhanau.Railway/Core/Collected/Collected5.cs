namespace Tsikhanau.Railway;

public readonly struct Collected<T1, T2, T3, T4, T5>
    where T1 : notnull
    where T2 : notnull
    where T3 : notnull
    where T4 : notnull
    where T5 : notnull
{
    private readonly Result<T1> _first;
    private readonly Result<T2> _second;
    private readonly Result<T3> _third;
    private readonly Result<T4> _fourth;
    private readonly Result<T5> _fifth;
    private readonly Boolean _initialized;

    internal Collected(Result<T1> first, Result<T2> second, Result<T3> third, Result<T4> fourth, Result<T5> fifth)
    {
        _first = first;
        _second = second;
        _third = third;
        _fourth = fourth;
        _fifth = fifth;
        _initialized = true;
    }

    public Collected<T1, T2, T3, T4, T5, T6> And<T6>(Result<T6> next)
        where T6 : notnull
    {
        Guard.AgainstUncollected(_initialized);
        return new Collected<T1, T2, T3, T4, T5, T6>(_first, _second, _third, _fourth, _fifth, next);
    }

    public Result<TOut> Map<TOut>(Func<T1, T2, T3, T4, T5, TOut> map)
        where TOut : notnull
    {
        Guard.AgainstNull(map);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        var second = _second;
        var third = _third;
        var fourth = _fourth;
        var fifth = _fifth;
        if (first.IsSuccess && second.IsSuccess && third.IsSuccess && fourth.IsSuccess
            && fifth.IsSuccess)
        {
            return Result<TOut>.Success(map(
                first.Value,
                second.Value,
                third.Value,
                fourth.Value,
                fifth.Value));
        }

        return Result<TOut>.Failure(Errors());
    }

    public Result<TOut> Bind<TOut>(Func<T1, T2, T3, T4, T5, Result<TOut>> bind)
        where TOut : notnull
    {
        Guard.AgainstNull(bind);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        var second = _second;
        var third = _third;
        var fourth = _fourth;
        var fifth = _fifth;
        if (first.IsSuccess && second.IsSuccess && third.IsSuccess && fourth.IsSuccess
            && fifth.IsSuccess)
        {
            return bind(
                first.Value,
                second.Value,
                third.Value,
                fourth.Value,
                fifth.Value);
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
        ErrorMerge.Add(ref errors, _fifth);
        return ErrorMerge.Merge(errors!);
    }
}
