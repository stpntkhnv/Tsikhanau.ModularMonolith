namespace Tsikhanau.Railway;

public readonly struct Collected<T1, T2, T3, T4, T5, T6, T7>
    where T1 : notnull
    where T2 : notnull
    where T3 : notnull
    where T4 : notnull
    where T5 : notnull
    where T6 : notnull
    where T7 : notnull
{
    private readonly Result<T1> _first;
    private readonly Result<T2> _second;
    private readonly Result<T3> _third;
    private readonly Result<T4> _fourth;
    private readonly Result<T5> _fifth;
    private readonly Result<T6> _sixth;
    private readonly Result<T7> _seventh;
    private readonly Boolean _initialized;

    internal Collected(Result<T1> first, Result<T2> second, Result<T3> third, Result<T4> fourth, Result<T5> fifth, Result<T6> sixth, Result<T7> seventh)
    {
        _first = first;
        _second = second;
        _third = third;
        _fourth = fourth;
        _fifth = fifth;
        _sixth = sixth;
        _seventh = seventh;
        _initialized = true;
    }

    public Collected<T1, T2, T3, T4, T5, T6, T7, T8> And<T8>(Result<T8> next)
        where T8 : notnull
    {
        Guard.AgainstUncollected(_initialized);
        return new Collected<T1, T2, T3, T4, T5, T6, T7, T8>(_first, _second, _third, _fourth, _fifth, _sixth, _seventh, next);
    }

    public Result<TOut> Map<TOut>(Func<T1, T2, T3, T4, T5, T6, T7, TOut> map)
        where TOut : notnull
    {
        Guard.AgainstNull(map);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        var second = _second;
        var third = _third;
        var fourth = _fourth;
        var fifth = _fifth;
        var sixth = _sixth;
        var seventh = _seventh;
        if (first.IsSuccess && second.IsSuccess && third.IsSuccess && fourth.IsSuccess
            && fifth.IsSuccess && sixth.IsSuccess && seventh.IsSuccess)
        {
            return Result<TOut>.Success(map(
                first.Value,
                second.Value,
                third.Value,
                fourth.Value,
                fifth.Value,
                sixth.Value,
                seventh.Value));
        }

        return Result<TOut>.Failure(Errors());
    }

    public Result<TOut> Bind<TOut>(Func<T1, T2, T3, T4, T5, T6, T7, Result<TOut>> bind)
        where TOut : notnull
    {
        Guard.AgainstNull(bind);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        var second = _second;
        var third = _third;
        var fourth = _fourth;
        var fifth = _fifth;
        var sixth = _sixth;
        var seventh = _seventh;
        if (first.IsSuccess && second.IsSuccess && third.IsSuccess && fourth.IsSuccess
            && fifth.IsSuccess && sixth.IsSuccess && seventh.IsSuccess)
        {
            return bind(
                first.Value,
                second.Value,
                third.Value,
                fourth.Value,
                fifth.Value,
                sixth.Value,
                seventh.Value);
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
        ErrorMerge.Add(ref errors, _sixth);
        ErrorMerge.Add(ref errors, _seventh);
        return ErrorMerge.Merge(errors!);
    }
}
