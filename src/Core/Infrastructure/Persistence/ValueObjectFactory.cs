using Tsikhanau.Monolith.Domain.ValueObjects;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

public static class ValueObjectFactory<TValueObject, TValue>
    where TValueObject : IValueObject<TValueObject, TValue>
    where TValue : notnull
{
    public static TValueObject Create(TValue value) => TValueObject.From(value);
}
