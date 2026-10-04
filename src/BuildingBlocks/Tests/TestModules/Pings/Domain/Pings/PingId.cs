using ModularMonolith.BuildingBlocks.Domain.Ids;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Domain.Pings;

internal readonly record struct PingId(Guid Value) : ITypedId<PingId>
{
    public static PingId New(DateTimeOffset createdOn) => new(Guid.CreateVersion7(createdOn));

    public static PingId From(Guid value) => new(value);

    public static Result<PingId> Parse(Guid value, String fieldName) =>
        value == Guid.Empty ? Error.Validation(fieldName, "Id is required") : new PingId(value);
}
