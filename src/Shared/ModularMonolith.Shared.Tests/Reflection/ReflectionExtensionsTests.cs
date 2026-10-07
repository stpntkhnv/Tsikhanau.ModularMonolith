using ModularMonolith.Shared.Reflection;
using ModularMonolith.Shared.Tests.Reflection.Scenarios;
using ModularMonolith.Shared.Tests.Reflection.Scenarios.Owner;

namespace ModularMonolith.Shared.Tests.Reflection;

public sealed class ReflectionExtensionsTests
{
    [Fact]
    public void Concrete_types_leave_out_abstract_classes_interfaces_and_open_generics()
    {
        var concreteTypes = typeof(ReflectionExtensionsTests).Assembly.ConcreteTypes;

        concreteTypes.ShouldContain(typeof(ConcreteGreeter));
        concreteTypes.ShouldNotContain(typeof(AbstractGreeter));
        concreteTypes.ShouldNotContain(typeof(IGreeter));
        concreteTypes.ShouldNotContain(typeof(Box<>));
    }

    [Fact]
    public void Instances_are_created_for_every_concrete_implementation()
    {
        var greetings = typeof(ReflectionExtensionsTests).Assembly
            .CreateInstancesOf<IGreeter>()
            .Select(greeter => greeter.Greet());

        greetings.ShouldBe(["hello", "hi"], ignoreOrder: true);
    }

    [Fact]
    public void Instances_are_created_only_for_concrete_implementations_in_the_namespace_of_the_type_or_below_it()
    {
        var partNames = typeof(OwnerPart)
            .CreateInstancesInItsNamespace<IPart>()
            .Select(part => part.Name);

        partNames.ShouldBe(["owner", "nested"], ignoreOrder: true);
    }

    [Fact]
    public void Type_without_a_namespace_creates_instances_for_every_implementation_in_its_assembly()
    {
        var partNames = typeof(GlobalPart)
            .CreateInstancesInItsNamespace<IPart>()
            .Select(part => part.Name);

        partNames.ShouldBe(["global", "owner", "nested", "neighbour"], ignoreOrder: true);
    }

    [Fact]
    public void Open_generic_contracts_are_recognised()
    {
        var implementsBox = typeof(IntBox).Implements(typeof(IBox<>));
        var boxInterfaces = typeof(IntBox).GetInterfacesBuiltFrom(typeof(IBox<>));
        var isBuiltFromBox = typeof(Box<Int32>).IsBuiltFrom(typeof(Box<>));
        var isListBuiltFromBox = typeof(List<Int32>).IsBuiltFrom(typeof(Box<>));

        implementsBox.ShouldBeTrue();
        boxInterfaces.ShouldBe([typeof(IBox<Int32>)]);
        isBuiltFromBox.ShouldBeTrue();
        isListBuiltFromBox.ShouldBeFalse();
    }

    [Fact]
    public void DerivesFrom_finds_an_open_generic_base_at_any_depth()
    {
        var grandchildDerivesFromBox = typeof(Grandchild).DerivesFrom(typeof(Box<>));
        var childDerivesFromBox = typeof(IntBox).DerivesFrom(typeof(Box<>));
        var unrelatedDerivesFromBox = typeof(ConcreteGreeter).DerivesFrom(typeof(Box<>));
        var boxDerivesFromItself = typeof(Box<Int32>).DerivesFrom(typeof(Box<>));

        grandchildDerivesFromBox.ShouldBeTrue();
        childDerivesFromBox.ShouldBeTrue();
        unrelatedDerivesFromBox.ShouldBeFalse();
        boxDerivesFromItself.ShouldBeFalse();
    }

    [Fact]
    public void Namespace_covers_its_own_types_and_nested_namespaces_only()
    {
        var isInRoot = typeof(String).IsInNamespace("System");
        var isInNested = typeof(List<Int32>).IsInNamespace("System");
        var isUnderPrefix = typeof(String).IsInNamespace("Sys");
        var isUnderSibling = typeof(String).IsInNamespace("System.Collections");

        isInRoot.ShouldBeTrue();
        isInNested.ShouldBeTrue();
        isUnderPrefix.ShouldBeFalse();
        isUnderSibling.ShouldBeFalse();
    }

    [Fact]
    public void Instance_is_created_through_a_non_public_constructor()
    {
        var greeter = typeof(GreeterWithPrivateConstructor).CreateInstance<IGreeter>();
        var greeting = greeter.Greet();

        greeting.ShouldBe("hi");
    }

    private interface IGreeter
    {
        String Greet();
    }

    private interface IBox<T>;

    private abstract class AbstractGreeter : IGreeter
    {
        public abstract String Greet();
    }

    private sealed class ConcreteGreeter : AbstractGreeter
    {
        public override String Greet() => "hello";
    }

    private sealed class GreeterWithPrivateConstructor : IGreeter
    {
        private GreeterWithPrivateConstructor() { }

        public String Greet() => "hi";
    }

    private class Box<T>;

    private sealed class IntBox : Box<Int32>, IBox<Int32>;

    private sealed class Grandchild : ChildBox;

    private class ChildBox : Box<Int32>;
}
