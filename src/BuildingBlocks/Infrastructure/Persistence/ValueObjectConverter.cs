using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ModularMonolith.BuildingBlocks.Domain.ValueObjects;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public sealed class ValueObjectConverter<TValueObject, TValue>() : ValueConverter<TValueObject, TValue>(
    valueObject => valueObject.Value,
    value => ValueObjectFactory<TValueObject, TValue>.Create(value))
    where TValueObject : IValueObject<TValueObject, TValue>
    where TValue : notnull;
