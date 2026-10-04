using ModularMonolith.BuildingBlocks.Domain.ValueObjects;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Domain.Pings;

internal readonly record struct PingLabel : IValueObject<PingLabel, String>
{
    public const Int32 MaxLength = 50;

    private PingLabel(String value) => Value = value;

    public String Value { get; }

    static PingLabel IValueObject<PingLabel, String>.From(String value) => new(value);

    public static Result<PingLabel> Create(String value, String fieldName) =>
        String.IsNullOrWhiteSpace(value) || value.Length > MaxLength
            ? Error.Validation(fieldName, $"Label must be 1 to {MaxLength} characters")
            : new PingLabel(value);
}
