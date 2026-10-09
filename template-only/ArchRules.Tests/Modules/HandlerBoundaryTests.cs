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
        var sampleModule = new ArchModule("Samples", SampleNamespace, [typeof(HandlerBoundaryTests).Assembly]);

        var evaluation = HandlerBoundaryRules.QueryHandlersTakeNoWriteSideDependencies(sampleModule);

        evaluation.Violations.ShouldContain($"{SampleQueryHandlerName}+OutboxWriter takes IOutbox`1 outbox");
    }

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
