namespace ModularMonolith.BuildingBlocks.Domain.ValueObjects;

public interface IValueObject;

public interface IValueObject<TSelf, TValue> : IValueObject
    where TSelf : IValueObject<TSelf, TValue>
    where TValue : notnull
{
    TValue Value { get; }

    static abstract TSelf From(TValue value);
}
