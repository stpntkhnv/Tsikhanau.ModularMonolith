using Tsikhanau.Monolith.Application.Commands;

namespace Acme.Modules.Bad.Features.StackCrate;

[InternalCommandName("stack-crate")]
internal sealed record StackCrateInternalCommand(Guid Id, Guid CrateId) : InternalCommand(Id);
