using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Persistence;

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

    [Theory]
    [InlineData(typeof(ISqlConnectionFactory<ISampleModule>))]
    [InlineData(typeof(IUnitOfWork<ISampleModule>))]
    [InlineData(typeof(IInternalCommandScheduler<ISampleModule>))]
    [InlineData(typeof(InternalCommandScheduler<ISampleModule>))]
    public void Listed_core_key_types_do_not_expose_the_module_argument(Type type) =>
        type.WithWrappedTypes
            .ShouldNotContain(typeof(ISampleModule));

    [Fact]
    public void Module_dispatcher_counts_as_the_facade() =>
        typeof(ModuleDispatcher<ISampleModule>).WithWrappedTypes
            .ShouldContain(typeof(ISampleModule));

    [Fact]
    public void Wrapper_outside_the_core_keeps_the_module_argument() =>
        typeof(Keyed<ISampleModule>).WithWrappedTypes
            .ShouldContain(typeof(ISampleModule));

    private sealed class Keyed<TModule>
        where TModule : IModuleFacade;

    private sealed class Sample;

    private interface ISampleModule : IModuleFacade;
}
