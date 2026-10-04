namespace Tsikhanau.Railway;

internal static class ErrorMerge
{
    public static Error Merge(List<Error> errors)
    {
        if (errors.Count == 1)
        {
            return errors[0];
        }

        if (errors.TrueForAll(static e => e is ValidationError))
        {
            return ValidationError.From(errors.SelectMany(static e => ((ValidationError)e).FieldErrors));
        }

        return AggregateError.From(errors);
    }

    public static void Add<T>(ref List<Error>? errors, Result<T> result)
        where T : notnull
    {
        if (result.IsFailure)
        {
            (errors ??= []).Add(result.Error);
        }
    }
}
