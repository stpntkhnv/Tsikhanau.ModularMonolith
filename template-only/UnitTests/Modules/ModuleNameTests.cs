using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Application.Modules;

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

    [Fact]
    public void A_module_class_whose_name_starts_with_a_lower_case_letter_is_refused_when_the_host_adds_it()
    {
        var builder = WebApplication.CreateSlimBuilder();

        var exception = Should.Throw<InvalidOperationException>(() => builder.AddModule<orderLinesModule>());

        exception.Message.ShouldBe(
            $"Module class {typeof(orderLinesModule).FullName} must be named <Module>Module, like NotesModule");
    }

    [Fact]
    public void A_module_class_and_its_facade_give_one_name_when_the_name_starts_with_an_i()
    {
        var moduleClassName = ModuleName.FromModuleClass(typeof(IAMModule));
        var facadeName = ModuleName.FromFacade(typeof(IIAMModule));

        moduleClassName.ShouldBe("IAM");
        facadeName.ShouldBe("IAM");
    }

    private interface IIAMModule : IModuleFacade;

    private sealed class OrderLinesModule : Module
    {
        protected override void AddServices(IServiceCollection services) { }
    }

    private sealed class OrderLines : Module
    {
        protected override void AddServices(IServiceCollection services) { }
    }

    private sealed class orderLinesModule : Module
    {
        protected override void AddServices(IServiceCollection services) { }
    }

    private sealed class IAMModule : Module
    {
        protected override void AddServices(IServiceCollection services) { }
    }
}
