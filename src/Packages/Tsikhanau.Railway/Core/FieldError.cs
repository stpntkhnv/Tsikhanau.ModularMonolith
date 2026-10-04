namespace Tsikhanau.Railway;

public readonly record struct FieldError
{
    public FieldError(String field, String message)
    {
        Field = Guard.AgainstNull(field);
        Message = Guard.AgainstNullOrWhiteSpace(message);
    }

    public String Field { get; }

    public String Message { get; }

    public override String ToString() => String.IsNullOrEmpty(Field) ? Message ?? String.Empty : $"{Field}: {Message}";
}
