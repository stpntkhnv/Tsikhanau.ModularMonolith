using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ModularMonolith.BuildingBlocks.Domain.Ids;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public sealed class TypedIdConverter<TId>()
    : ValueConverter<TId, Guid>(id => id.Value, value => TypedIdFactory<TId>.Create(value))
    where TId : struct, ITypedId<TId>;
