using ModularMonolith.BuildingBlocks.Application.Queries;
using ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios.Foreign;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios.Owner;

public sealed class ForeignQueryHandler : IQueryHandler<ForeignQuery, Int32>
{
    public Task<Result<Int32>> Handle(ForeignQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(0));
}
