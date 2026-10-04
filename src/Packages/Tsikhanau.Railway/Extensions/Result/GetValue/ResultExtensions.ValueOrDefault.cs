namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static T? GetValueOrDefault<T>(this Result<T> result)
        where T : notnull
    {
        return result.IsSuccess ? result.Value : default;
    }

    public static T GetValueOrDefault<T>(
        this Result<T> result,
        T defaultValue)
        where T : notnull
    {
        return result.IsSuccess ? result.Value : defaultValue;
    }

    public static T GetValueOrDefault<T>(
        this Result<T> result,
        Func<T> defaultValueFactory)
        where T : notnull
    {
        return result.IsSuccess ? result.Value : defaultValueFactory();
    }
}
