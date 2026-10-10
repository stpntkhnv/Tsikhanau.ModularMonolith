using Acme.SharedKernel;

namespace Acme.Modules.Bad.Contracts.Queries.CrateWeight;

public sealed record CrateWeightDto(Guid CrateId, Weight Weight);
