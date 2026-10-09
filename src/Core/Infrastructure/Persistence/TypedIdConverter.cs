using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tsikhanau.Monolith.Domain.Ids;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

public sealed class TypedIdConverter<TId>()
    : ValueConverter<TId, Guid>(id => id.Value, value => TypedIdFactory<TId>.Create(value))
    where TId : struct, ITypedId<TId>;
