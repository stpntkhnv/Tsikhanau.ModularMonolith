namespace Tsikhanau.Railway;

public readonly struct Collected<T1>
    where T1 : notnull
{
    private readonly Result<T1> _first;
    private readonly Boolean _initialized;

    internal Collected(Result<T1> first)
    {
        _first = first;
        _initialized = true;
    }

    public Collected<T1, T2> And<T2>(Result<T2> next)
        where T2 : notnull
    {
        Guard.AgainstUncollected(_initialized);
        return new Collected<T1, T2>(_first, next);
    }

    public Result<TOut> Map<TOut>(Func<T1, TOut> map)
        where TOut : notnull
    {
        Guard.AgainstNull(map);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        if (first.IsSuccess)
        {
            return Result<TOut>.Success(map(first.Value));
        }

        return Result<TOut>.Failure(Errors());
    }

    public Result<TOut> Bind<TOut>(Func<T1, Result<TOut>> bind)
        where TOut : notnull
    {
        Guard.AgainstNull(bind);
        Guard.AgainstUncollected(_initialized);
        var first = _first;
        if (first.IsSuccess)
        {
            return bind(first.Value);
        }

        return Result<TOut>.Failure(Errors());
    }

    private Error Errors()
    {
        List<Error>? errors = null;
        ErrorMerge.Add(ref errors, _first);
        return ErrorMerge.Merge(errors!);
    }
}
