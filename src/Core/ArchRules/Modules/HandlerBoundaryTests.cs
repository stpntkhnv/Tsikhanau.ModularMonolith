using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class HandlerBoundaryTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Theory]
    [EveryModule]
    public void Module_takes_no_handler_in_a_constructor(String name) =>
        Check(HandlerBoundaryRules.ModuleTakesNoHandlerInAConstructor(GetModule(name)), $"{name} has no types");

    [Theory]
    [EveryModule]
    public void Query_marker_is_implemented_only_inside_its_module(String name) =>
        Check(
            HandlerBoundaryRules.QueryMarkerIsImplementedOnlyInsideItsModule(Modules, GetModule(name)),
            $"{name} has no types");

    [Theory]
    [EveryModule]
    public void Query_handlers_take_no_write_side_dependencies(String name) =>
        Check(
            HandlerBoundaryRules.QueryHandlersTakeNoWriteSideDependencies(GetModule(name)),
            $"{name} has no query handlers",
            "A query handler only reads; take ISqlConnectionFactory, not the write side");

    [Theory]
    [EveryModule]
    public void Only_endpoints_and_background_jobs_take_the_module_facade(String name) =>
        Check(
            HandlerBoundaryRules.OnlyEndpointsAndBackgroundJobsTakeTheModuleFacade(GetModule(name)),
            $"{name} has no Domain or Features types",
            "A handler that calls its own facade leaves its transaction; call the domain directly");
}
