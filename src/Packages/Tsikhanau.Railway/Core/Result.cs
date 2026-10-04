using System.Diagnostics;

namespace Tsikhanau.Railway;

[DebuggerDisplay("{ToString(),nq}")]
public readonly struct Result<T> : IEquatable<Result<T>>
    where T : notnull
{
    private readonly T? _value;
    private readonly Error? _error;
    private readonly Boolean _isSuccess;

    private Result(T value)
    {
        _value = value;
        _error = null;
        _isSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
        _isSuccess = false;
    }

    public Boolean IsSuccess => _isSuccess;

    public Boolean IsFailure => !_isSuccess;

    public T Value => _isSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value on a failed result. Error: {Describe(_error)}");

    public Error Error => _error
        ?? throw new InvalidOperationException(_isSuccess
            ? "Cannot access Error on a successful result."
            : "Result is not initialized. Create it with Result.Success or Result.Failure.");

    public static Result<T> Success(T value)
    {
        Guard.AgainstNull(value);
        return new Result<T>(value);
    }

    public static Result<T> Failure(Error error)
    {
        Guard.AgainstNull(error);
        return new Result<T>(error);
    }

    public Boolean Equals(Result<T> other)
    {
        if (_isSuccess != other._isSuccess)
        {
            return false;
        }

        return _isSuccess
            ? EqualityComparer<T>.Default.Equals(_value, other._value)
            : Equals(_error, other._error);
    }

    public override Boolean Equals(Object? obj) => obj is Result<T> other && Equals(other);

    public override Int32 GetHashCode() => _isSuccess
        ? HashCode.Combine(true, _value)
        : HashCode.Combine(false, _error);

    public override String ToString() => _isSuccess
        ? $"Success({_value})"
        : _error is null ? "Uninitialized" : $"Failure({_error})";

    public static Boolean operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    public static Boolean operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);

    private static String Describe(Error? error) => error?.ToString() ?? "not initialized";
}
