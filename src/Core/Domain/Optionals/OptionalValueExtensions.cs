using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Domain.Optionals;

public static class OptionalValueExtensions
{
    extension<T>(T? value)
        where T : struct
    {
        public Optional<T> AsOptional() => value.HasValue ? Optional<T>.Some(value.Value) : Optional<T>.None();
    }

    extension<T>(Optional<T> optional)
        where T : struct
    {
        public T? AsNullable() => optional.HasValue ? optional.Value : null;
    }

    extension<T>(Optional<T> optional)
        where T : notnull
    {
        public IEnumerable<T> AsEnumerable() => optional.HasValue ? [optional.Value] : [];
    }
}
