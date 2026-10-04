namespace Tsikhanau.Railway;

public sealed record AggregateError : Error
{
    public const String DefaultCode = "aggregate";

    private AggregateError(Error[] errors)
        : base(SharedKind(errors), DefaultCode, String.Join("; ", errors.Select(e => e.Message)))
    {
        Errors = Array.AsReadOnly(errors);
    }

    public IReadOnlyList<Error> Errors { get; }

    public static AggregateError From(IEnumerable<Error> errors)
    {
        Guard.AgainstNull(errors);

        var array = errors.ToArray();
        if (array.Length == 0)
            throw new ArgumentException("AggregateError must contain at least one error.", nameof(errors));

        foreach (var error in array)
        {
            if (error is null)
                throw new ArgumentException("Errors cannot contain null.", nameof(errors));
        }

        return new AggregateError(array);
    }

    public Boolean Equals(AggregateError? other) =>
        other is not null && base.Equals(other) && Errors.SequenceEqual(other.Errors);

    public override Int32 GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());
        foreach (var error in Errors)
        {
            hash.Add(error);
        }

        return hash.ToHashCode();
    }

    private static ErrorKind SharedKind(Error[] errors)
    {
        var kind = errors[0].Kind;
        foreach (var error in errors)
        {
            if (error.Kind != kind)
                return ErrorKind.Failure;
        }

        return kind;
    }
}
