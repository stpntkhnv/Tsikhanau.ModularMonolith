namespace Tsikhanau.Railway;

public readonly struct Optional<T> : IEquatable<Optional<T>>
    where T : notnull
{
    private readonly T? _value;
    private readonly Boolean _hasValue;

    private Optional(T value)
    {
        _value = value;
        _hasValue = true;
    }

    public Boolean HasValue => _hasValue;

    public Boolean IsNone => !_hasValue;

    public T Value => _hasValue ? _value! : throw new InvalidOperationException("Cannot access Value on an empty optional.");

    public static Optional<T> Some(T value)
    {
        Guard.AgainstNull(value);
        return new Optional<T>(value);
    }

    public static Optional<T> None() => default;

    public static Optional<T> FromNullable(T? value) => value is null ? default : new Optional<T>(value);

    public T? GetValueOrDefault() => _value;

    public T GetValueOrDefault(T defaultValue) => _hasValue ? _value! : defaultValue;

    public T GetValueOrDefault(Func<T> defaultValueFactory)
    {
        Guard.AgainstNull(defaultValueFactory);
        return _hasValue ? _value! : defaultValueFactory();
    }

    public Boolean Equals(Optional<T> other)
    {
        if (_hasValue != other._hasValue)
            return false;

        return !_hasValue || EqualityComparer<T>.Default.Equals(_value, other._value);
    }

    public override Boolean Equals(Object? obj) => obj is Optional<T> other && Equals(other);

    public override Int32 GetHashCode() => _hasValue ? _value!.GetHashCode() : 0;

    public override String ToString() => _hasValue ? $"Some({_value})" : "None";

    public static Boolean operator ==(Optional<T> left, Optional<T> right) => left.Equals(right);

    public static Boolean operator !=(Optional<T> left, Optional<T> right) => !left.Equals(right);

    public static implicit operator Optional<T>(T? value) => FromNullable(value);
}
