using Tsikhanau.Monolith.Application.Commands;

namespace Acme.Modules.Bad.Features.UnstackCrate;

[InternalCommandName("stack-crate")]
internal sealed record UnstackCrateInternalCommand(Guid Id, Guid CrateId) : InternalCommand(Id);
