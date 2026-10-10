using Tsikhanau.Monolith.Domain.ValueObjects;

namespace Acme.Modules.Bad.Domain.Grades;

internal readonly record struct Grade : IValueObject<Grade, Int32>
{
    private Grade(Int32 value) => Value = value;

    public Int32 Value { get; init; }

    static Grade IValueObject<Grade, Int32>.From(Int32 value) => new(value);
}
