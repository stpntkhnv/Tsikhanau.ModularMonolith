using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Tests.Inputs;
using Tsikhanau.Monolith.Contracts.Queries;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.ArchRules.Tests.Modules;

public sealed class HandlerBoundaryTests : HandlerBoundaryTests<TestModuleInputs>
{
    private const String SampleNamespace = "Tsikhanau.Monolith.ArchRules.Tests.Modules";
    private const String SampleQueryHandlerName = $"{SampleNamespace}.HandlerBoundaryTests+SampleQueryHandler";

    [Fact]
    public void Write_side_taken_by_a_type_nested_in_a_query_handler_is_checked()
    {
        var evaluation = HandlerBoundaryRules.QueryHandlersTakeNoWriteSideDependencies(SampleModule);

        evaluation.Violations.ShouldContain($"{SampleQueryHandlerName}+OutboxWriter takes IOutbox`1 outbox");
    }

    [Fact]
    public void Query_marker_rule_is_silent_on_a_module_without_a_query_marker()
    {
        var sampleModule = SampleModule;

        var evaluation = HandlerBoundaryRules.QueryMarkerIsImplementedOnlyInsideItsModule(
            new ArchModules([sampleModule]),
            sampleModule);

        evaluation.Violations.ShouldBeEmpty();
    }

    private static ArchModule SampleModule =>
        new("Samples", SampleNamespace, () => [typeof(HandlerBoundaryTests).Assembly]);

    private interface ISampleModule : IModuleFacade;

    private sealed record SampleQuery : IQuery<String>;

    private sealed class SampleQueryHandler : IQueryHandler<SampleQuery, String>
    {
        public Task<Result<String>> Handle(SampleQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<Result<String>>("read");

        private sealed class OutboxWriter(IOutbox<ISampleModule> outbox)
        {
            public IOutbox<ISampleModule> Outbox => outbox;
        }
    }
}
