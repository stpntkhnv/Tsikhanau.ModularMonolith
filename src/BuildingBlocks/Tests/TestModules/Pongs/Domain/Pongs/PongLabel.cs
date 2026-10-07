using ModularMonolith.BuildingBlocks.Domain.ValueObjects;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pongs.Domain.Pongs;

internal readonly record struct PongLabel : IValueObject<PongLabel, String>
{
    public const Int32 MaxLength = 50;

    public static readonly PongLabel Secret = new("secret");

    private PongLabel(String value) => Value = value;

    public String Value { get; }

    static PongLabel IValueObject<PongLabel, String>.From(String value) => new(value);

    public static Result<PongLabel> Create(String value, String fieldName) =>
        String.IsNullOrWhiteSpace(value) || value.Length > MaxLength
            ? Error.Validation(fieldName, $"Label must be 1 to {MaxLength} characters")
            : new PongLabel(value);
}
