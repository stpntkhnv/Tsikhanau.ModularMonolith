using ModularMonolith.BuildingBlocks.Domain.ValueObjects;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public static class ValueObjectFactory<TValueObject, TValue>
    where TValueObject : IValueObject<TValueObject, TValue>
    where TValue : notnull
{
    public static TValueObject Create(TValue value) => TValueObject.From(value);
}
