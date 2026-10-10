using Tsikhanau.Monolith.Domain.ValueObjects;

namespace Acme.Modules.Bad.Domain.Scores;

internal readonly record struct Score : IValueObject<Score, Int32>
{
    private Score(Int32 value) => Value = value;

    public Int32 Value { get; }

    public static Score From(Int32 value) => new(value);
}
