using Tsikhanau.Monolith.Domain.Optionals;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Domain;

public sealed class OptionalValueExtensionsTests
{
    [Fact]
    public void Nullable_value_becomes_an_optional_and_back()
    {
        Int32? someNullable = 5;
        Int32? noneNullable = null;

        var someOptional = someNullable.AsOptional();
        var noneOptional = noneNullable.AsOptional();
        var roundTrippedSome = someOptional.AsNullable();
        var roundTrippedNone = noneOptional.AsNullable();

        someOptional.ShouldBe(Optional<Int32>.Some(5));
        noneOptional.ShouldBe(Optional<Int32>.None());
        roundTrippedSome.ShouldBe(5);
        roundTrippedNone.ShouldBeNull();
    }

    [Fact]
    public void Optional_reads_as_a_sequence_of_zero_or_one()
    {
        var someOptional = Optional<String>.Some("a");
        var noneOptional = Optional<String>.None();

        var someSequence = someOptional.AsEnumerable();
        var noneSequence = noneOptional.AsEnumerable();

        someSequence.ShouldBe(["a"]);
        noneSequence.ShouldBeEmpty();
    }
}
