using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Modules;

namespace Tsikhanau.Monolith.UnitTests.Modules;

public sealed class ModuleNameTests
{
    [Fact]
    public void Module_name_is_the_class_name_without_the_module_suffix()
    {
        var orderLinesModule = new OrderLinesModule();

        orderLinesModule.Name.ShouldBe("OrderLines");
    }

    [Fact]
    public void A_module_class_without_the_module_suffix_is_refused_when_the_host_adds_it()
    {
        var builder = WebApplication.CreateSlimBuilder();

        var exception = Should.Throw<InvalidOperationException>(() => builder.AddModule<OrderLines>());

        exception.Message.ShouldBe(
            $"Module class {typeof(OrderLines).FullName} must be named <Module>Module, like NotesModule");
    }

    private sealed class OrderLinesModule : Module
    {
        protected override void AddServices(IServiceCollection services) { }
    }

    private sealed class OrderLines : Module
    {
        protected override void AddServices(IServiceCollection services) { }
    }
}
