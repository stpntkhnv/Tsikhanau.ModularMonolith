using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.ArchRules.Support;

namespace Tsikhanau.Monolith.ArchRules.Tests.Modules;

public sealed class WrappedTypeTests
{
    [Theory]
    [InlineData(typeof(Sample[]))]
    [InlineData(typeof(List<Sample>))]
    [InlineData(typeof(Func<Sample>))]
    [InlineData(typeof(Dictionary<String, Lazy<Sample[]>>))]
    public void Element_types_and_type_arguments_are_unwrapped(Type type) =>
        type.WithWrappedTypes
            .ShouldContain(typeof(Sample));

    [Fact]
    public void By_ref_types_are_unwrapped() =>
        typeof(Sample)
            .MakeByRefType()
            .WithWrappedTypes
            .ShouldContain(typeof(Sample));

    [Fact]
    public void Type_argument_that_names_a_module_is_not_unwrapped() =>
        typeof(ISqlConnectionFactory<ISampleModule>).WithWrappedTypes
            .ShouldBe([typeof(ISqlConnectionFactory<ISampleModule>)]);

    private sealed class Sample;

    private interface ISampleModule : IModuleFacade;
}
