namespace Tsikhanau.Railway;

public sealed record ValidationError : Error
{
    public const String DefaultCode = "validation";

    private ValidationError(FieldError[] fieldErrors)
        : base(ErrorKind.Validation, DefaultCode, String.Join("; ", fieldErrors))
    {
        FieldErrors = Array.AsReadOnly(fieldErrors);
    }

    public IReadOnlyList<FieldError> FieldErrors { get; }

    public static ValidationError For(String field, String message) => new([new FieldError(field, message)]);

    public static ValidationError From(IEnumerable<FieldError> fieldErrors)
    {
        Guard.AgainstNull(fieldErrors);

        var array = fieldErrors.ToArray();
        if (array.Length == 0)
            throw new ArgumentException("ValidationError must contain at least one field error.", nameof(fieldErrors));

        foreach (var fieldError in array)
        {
            if (fieldError.Message is null)
                throw new ArgumentException("Field error is not initialized.", nameof(fieldErrors));
        }

        return new ValidationError(array);
    }

    public ValidationError Merge(ValidationError other)
    {
        Guard.AgainstNull(other);
        return new ValidationError([.. FieldErrors, .. other.FieldErrors]);
    }

    public Boolean Equals(ValidationError? other) =>
        other is not null && base.Equals(other) && FieldErrors.SequenceEqual(other.FieldErrors);

    public override Int32 GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());
        foreach (var fieldError in FieldErrors)
        {
            hash.Add(fieldError);
        }

        return hash.ToHashCode();
    }
}
