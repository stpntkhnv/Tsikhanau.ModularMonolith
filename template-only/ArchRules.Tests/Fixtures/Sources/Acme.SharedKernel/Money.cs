using Tsikhanau.Monolith.Domain.ValueObjects;
using Tsikhanau.Railway;

namespace Acme.SharedKernel;

public readonly record struct Money : IValueObject<Money, Decimal>
{
    private Money(Decimal value) => Value = value;

    public Decimal Value { get; }

    static Money IValueObject<Money, Decimal>.From(Decimal value) => new(value);

    public static Result<Money> Create(Decimal value, String fieldName) =>
        value < 0 ? Error.Validation(fieldName, "Amount cannot be negative") : new Money(value);
}
