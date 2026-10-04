namespace Tsikhanau.Railway;

public record Error
{
    public Error(ErrorKind kind, String code, String message)
    {
        Kind = kind;
        Code = Guard.AgainstNullOrWhiteSpace(code);
        Message = Guard.AgainstNullOrWhiteSpace(message);
    }

    public ErrorKind Kind { get; }

    public String Code { get; }

    public String Message { get; }

    public Error? Inner { get; init; }

    public IReadOnlyDictionary<String, Object?>? Metadata { get; init; }

    public static Error Failure(String code, String message) => new(ErrorKind.Failure, code, message);

    public static Error Unexpected(String code, String message) => new(ErrorKind.Unexpected, code, message);

    public static Error NotFound(String code, String message) => new(ErrorKind.NotFound, code, message);

    public static Error Conflict(String code, String message) => new(ErrorKind.Conflict, code, message);

    public static Error Unauthorized(String code, String message) => new(ErrorKind.Unauthorized, code, message);

    public static Error Forbidden(String code, String message) => new(ErrorKind.Forbidden, code, message);

    public static ValidationError Validation(String field, String message) => ValidationError.For(field, message);

    public static Error FromException(Exception exception)
    {
        Guard.AgainstNull(exception);

        return new Error(
            ErrorKind.Unexpected,
            exception.GetType().Name,
            String.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message)
        {
            Inner = exception.InnerException is null ? null : FromException(exception.InnerException)
        };
    }

    public Error WithContext(String code, String message) => new(Kind, code, message) { Inner = this };

    public Error WithMetadata(String key, Object? value)
    {
        Guard.AgainstNull(key);

        var metadata = Metadata is null
            ? new Dictionary<String, Object?>(1)
            : new Dictionary<String, Object?>(Metadata);
        metadata[key] = value;

        return this with { Metadata = metadata };
    }

    public String GetFullMessage() => Inner is null
        ? Message
        : $"{Message} -> {Inner.GetFullMessage()}";

    public IEnumerable<String> GetAllCodes()
    {
        for (var error = this; error is not null; error = error.Inner)
        {
            yield return error.Code;
        }
    }

    public virtual Boolean Equals(Error? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && EqualityContract == other.EqualityContract
            && Kind == other.Kind
            && String.Equals(Code, other.Code, StringComparison.Ordinal)
            && String.Equals(Message, other.Message, StringComparison.Ordinal)
            && Equals(Inner, other.Inner)
            && MetadataEquals(Metadata, other.Metadata);
    }

    public override Int32 GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EqualityContract);
        hash.Add(Kind);
        hash.Add(Code, StringComparer.Ordinal);
        hash.Add(Message, StringComparer.Ordinal);
        hash.Add(Inner);
        hash.Add(Metadata?.Count ?? 0);
        return hash.ToHashCode();
    }

    public sealed override String ToString() => Inner is null
        ? $"[{Code}] {Message}"
        : $"[{Code}] {Message} -> {Inner}";

    private static Boolean MetadataEquals(IReadOnlyDictionary<String, Object?>? left, IReadOnlyDictionary<String, Object?>? right)
    {
        var leftCount = left?.Count ?? 0;
        var rightCount = right?.Count ?? 0;
        if (leftCount != rightCount)
        {
            return false;
        }

        if (leftCount == 0)
        {
            return true;
        }

        foreach (var (key, value) in left!)
        {
            if (!right!.TryGetValue(key, out var otherValue) || !Equals(value, otherValue))
            {
                return false;
            }
        }

        return true;
    }
}
