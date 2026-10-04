namespace Tsikhanau.Railway;

public static partial class Result
{
    public static Collected<T1> Collect<T1>(Result<T1> first)
        where T1 : notnull =>
        new(first);
}
